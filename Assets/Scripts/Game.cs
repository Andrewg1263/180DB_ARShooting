#define DEBUG
using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using uPLibrary.Networking.M2Mqtt.Utility;
using uPLibrary.Networking.M2Mqtt.Exceptions;
using System;
using IBM.Watson.SpeechToText.V1;
using IBM.Cloud.SDK;
using IBM.Cloud.SDK.Authentication;
using IBM.Cloud.SDK.Authentication.Iam;
using IBM.Cloud.SDK.Utilities;
using IBM.Cloud.SDK.DataTypes;

[RequireComponent(typeof(AudioSource))]


public class Game : MonoBehaviour
{
    [SerializeField] VideoClip losingClip;
    [SerializeField] VideoClip winningClip;
    [SerializeField] GameObject gameCamera;
    [SerializeField] GameObject Cross;
    [SerializeField] GameObject ResetButton;
    [SerializeField] GameObject Information;
    [SerializeField] GameObject Message;
    [SerializeField] Text healthDisplay;
    [SerializeField] Text ammoDisplay;
    [SerializeField] Text TestDisplay;
    
    VideoPlayer videoPlayer;
    Information Info_Stored;
    RaycastHit hit;
    MqttClient client;
    Player m_player;
    Button reset;
    Text EndMessage;
    string IP_address;// = "192.168.31.238";
    //string IP_address = "192.168.1.13";
    string topic;
    string from_myself;
    string from_enemy;
    string msgReceived;
    float nextFire;
    float fireRate; // number in seconds, how often can player shoot
    float weaponRange;
    bool gameOver;
    bool imAlive;
    bool enemyAlive;
    bool charged;
    int gunDamage;

    // for speech recognition
    #region PLEASE SET THESE VARIABLES IN THE INSPECTOR
    [Space(10)]
    [Tooltip("The service URL (optional). This defaults to \"https://api.us-south.speech-to-text.watson.cloud.ibm.com/instances/d16813dc-e25c-4f9f-b7c1-f7eb9ad81c14\"")]
    [SerializeField]
    private string _serviceUrl;
    //[Tooltip("Text field to display the results of streaming.")]
    //public Text ResultsField;
    [Header("IAM Authentication")]
    [Tooltip("The IAM apikey.")]
    [SerializeField]
    private string _iamApikey;

    [Header("Parameters")]
    // https://www.ibm.com/watson/developercloud/speech-to-text/api/v1/curl.html?curl#get-model
    [Tooltip("The Model to use. This defaults to en-US_BroadbandModel")]
    [SerializeField] string _recognizeModel;
    int _recordingRoutine = 0;
    string _microphoneID = null;
    AudioClip _recording = null;
    private int _recordingBufferSize = 1;
    private int _recordingHZ = 22050;
    SpeechToTextService _service;
    string voiceCommand;
    // speech recognition declaration ends
    #endregion

    void Start()
    {
        // Initialize Variables
        videoPlayer = gameCamera.GetComponentInChildren<VideoPlayer>();
        imAlive = true;
        enemyAlive = true;
        gameOver = false;
        charged = false;
        nextFire = 0f;
        fireRate = 0.25f;
        gunDamage = 1;
        weaponRange = 5f;
        m_player = GetComponentInChildren<Player>();
        Info_Stored = Information.GetComponent<Information>();

        // Initialize Display Elements
        UpdateStats();
        ResetButton.SetActive(false);
        reset = ResetButton.GetComponent<Button>();
        reset.onClick.AddListener(Reset);
        EndMessage = Message.GetComponent<Text>();
        Message.SetActive(false);

        // MQTT Setup
        from_myself = Info_Stored.Get_PlayerNum();
        from_enemy = Info_Stored.Get_EnemyNum();
        IP_address = Info_Stored.Get_IP();
        
        msgReceived = "Default";
        client = new MqttClient(IPAddress.Parse(IP_address), 1883, false, null); 
        client.MqttMsgPublishReceived += client_MqttMsgPublishReceived;
        string clientId = Guid.NewGuid().ToString();
        client.Connect(clientId);
        string[] topics = new string[] { "Status "+from_enemy, "RPiCommand "+from_myself, "Damage "+from_enemy};
        byte[] QOS_levels = new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE };
        client.Subscribe(topics, QOS_levels);
        Send("Status " + from_myself, "alive");
    }

    void client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
    {
        msgReceived = System.Text.Encoding.UTF8.GetString(e.Message);
        topic = e.Topic;
    }

    void Send(string topic, string msg)
    {
        byte[] payload = System.Text.Encoding.UTF8.GetBytes(msg);
        client.Publish(topic, payload, MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, true);
    }

    void Update()
    {
        Debug.Log( "Raycast : " + Physics.Raycast(gameCamera.transform.position, gameCamera.transform.forward, out hit));
        if(!gameOver)
        {
            Action("", voiceCommand);
            if (topic == "Status " + from_enemy)
            {
                // Receive opponent status: dead/alive
                if (msgReceived == "dead")
                {
                    if (imAlive)
                    {
                        enemyAlive = false;
                    }
                    gameOver = true;
                }
            }
            else if (topic == "RPiCommand " + from_myself) // Receive from RPi and Send damageAmount to opponent
            {
                // Print(msgReceived, topic);
                Action(msgReceived, "");
                UpdateStats();
            }
            else if (topic == "Damage " + from_enemy)
            {
                // print("Received Damage");
                bool validDamage = int.TryParse(msgReceived, out int damageReceived);
                if (validDamage)
                {
                    int newHealth = m_player.ReduceHealthBy(damageReceived);
                    UpdateStats();
                    if (newHealth <= 0)
                    {
                        gameOver = true;
                        imAlive = false;
                        Send("Status " + from_myself, "dead");
                    }
                    else
                    {
                        Send("Status " + from_myself, "alive");
                    }
                }
            }   
        }
        else
        {
            ResetButton.SetActive(true);
            Cross.SetActive(false);
            DisplayEndMessage();
            PlayEndVideo();
        }
        topic = "";
        msgReceived = "";
    }

    void DisplayEndMessage()
    {
        Message.SetActive(true);
        if(enemyAlive)
            EndMessage.text = from_enemy + " has won.";
        else
            EndMessage.text = "You have won.";
    }

    void Reset()
    {
        videoPlayer.Stop();
        gameOver = false;
        imAlive = true;
        m_player.Reset();
        UpdateStats();
        Send("Status " + from_myself, "alive");
        Send("Status " + from_myself, "Ready");
        enemyAlive = true;
        Cross.SetActive(true);
        ResetButton.SetActive(false);
        Message.SetActive(false);
    }

    void Action(string command, string voiceCommand)
    {
        switch (command)
        {
            case "1":
                {
                    bool scaleIsValid = int.TryParse(command, out int scale);
                    if(charged && scaleIsValid) { scale *= 3; }
                    bool enoughAmmo = m_player.CheckReq(scale);
                    if (scaleIsValid && enoughAmmo)
                        ShootUsing(scale);
                    break;
                }
            case "3": { charged = true; break; }
            case "2": { m_player.Reload(); break; }
            default: { break; }
        }
        switch(voiceCommand)
        {
            case "4": { m_player.ActivateShield(); break; }
            case "5": { m_player.UseAntibiotics(); break; }
            case "6": { gameOver = true; break; }
            default: { break; }
        }
    }

    void ShootUsing(int scale)
    {
        bool enemyDetected = Physics.Raycast(gameCamera.transform.position, gameCamera.transform.forward, out hit);
        float distance = hit.distance;
        bool time_req = Time.time > nextFire;
        bool ammo_req = m_player.Get_Ammo() > 0;
        bool distance_req = distance < weaponRange;

        if (time_req && ammo_req)
        {
            if (/*distance_req &&*/ enemyDetected)
            {
                string payload = (scale * gunDamage).ToString();
                Send("Damage " + from_myself, payload);
            }
            m_player.ReduceAmmoBy(scale);
            nextFire = Time.time + scale * fireRate;
            if (charged) { charged = false; }
        }
    }

    void PlayEndVideo()
    {
        if (!imAlive)
            videoPlayer.clip = losingClip;
        else
            videoPlayer.clip = winningClip;
        videoPlayer.Play();
    }

    void UpdateStats()
    {
        int newAmmo = m_player.Get_Ammo();
        int newHealth = m_player.Get_Health();
        int newShield = m_player.Get_Shield();
        string healthString = "Health: " + newHealth.ToString();
        if (newShield > 0)
            healthString += " ( +" + newShield.ToString() + " )";
        healthDisplay.text = healthString;
        ammoDisplay.text = "Ammo : " + newAmmo.ToString();
    }

    #region Speech Recognition 
    private IEnumerator CreateService()
    {
        if (string.IsNullOrEmpty(_iamApikey))
        {
            throw new IBMException("Plesae provide IAM ApiKey for the service.");
        }

        IamAuthenticator authenticator = new IamAuthenticator(apikey: _iamApikey);

        //  Wait for tokendata
        while (!authenticator.CanAuthenticate())
            yield return null;

        _service = new SpeechToTextService(authenticator);
        if (!string.IsNullOrEmpty(_serviceUrl))
        {
            _service.SetServiceUrl(_serviceUrl);
        }
        _service.StreamMultipart = true;

        Active = true;
        StartRecording();
    }

    public bool Active
    {
        get { return _service.IsListening; }
        set
        {
            if (value && !_service.IsListening)
            {
                _service.RecognizeModel = (string.IsNullOrEmpty(_recognizeModel) ? "en-US_BroadbandModel" : _recognizeModel);
                _service.DetectSilence = true;
                _service.EnableWordConfidence = true;
                _service.EnableTimestamps = true;
                _service.SilenceThreshold = 0.01f;
                _service.MaxAlternatives = 1;
                _service.EnableInterimResults = true;
                _service.OnError = OnError;
                _service.InactivityTimeout = -1;
                _service.ProfanityFilter = false;
                _service.SmartFormatting = true;
                _service.SpeakerLabels = false;
                _service.WordAlternativesThreshold = null;
                _service.EndOfPhraseSilenceTime = null;
                _service.StartListening(OnRecognize, OnRecognizeSpeaker);
            }
            else if (!value && _service.IsListening)
            {
                _service.StopListening();
            }
        }
    }

    private void StartRecording()
    {
        if (_recordingRoutine == 0)
        {
            UnityObjectUtil.StartDestroyQueue();
            _recordingRoutine = Runnable.Run(RecordingHandler());
        }
    }

    private void StopRecording()
    {
        if (_recordingRoutine != 0)
        {
            Microphone.End(_microphoneID);
            Runnable.Stop(_recordingRoutine);
            _recordingRoutine = 0;
        }
    }

    private void OnError(string error)
    {
        Active = false;

        Log.Debug("ExampleStreaming.OnError()", "Error! {0}", error);
    }

    private IEnumerator RecordingHandler()
    {
        Log.Debug("ExampleStreaming.RecordingHandler()", "devices: {0}", Microphone.devices);
        _recording = Microphone.Start(_microphoneID, true, _recordingBufferSize, _recordingHZ);
        yield return null;      // let _recordingRoutine get set..

        if (_recording == null)
        {
            StopRecording();
            yield break;
        }

        bool bFirstBlock = true;
        int midPoint = _recording.samples / 2;
        float[] samples = null;

        while (_recordingRoutine != 0 && _recording != null)
        {
            int writePos = Microphone.GetPosition(_microphoneID);
            if (writePos > _recording.samples || !Microphone.IsRecording(_microphoneID))
            {
                Log.Error("ExampleStreaming.RecordingHandler()", "Microphone disconnected.");

                StopRecording();
                yield break;
            }

            if ((bFirstBlock && writePos >= midPoint)
              || (!bFirstBlock && writePos < midPoint))
            {
                // front block is recorded, make a RecordClip and pass it onto our callback.
                samples = new float[midPoint];
                _recording.GetData(samples, bFirstBlock ? 0 : midPoint);

                AudioData record = new AudioData();
                record.MaxLevel = Mathf.Max(Mathf.Abs(Mathf.Min(samples)), Mathf.Max(samples));
                record.Clip = AudioClip.Create("Recording", midPoint, _recording.channels, _recordingHZ, false);
                record.Clip.SetData(samples, 0);

                _service.OnListen(record);

                bFirstBlock = !bFirstBlock;
            }
            else
            {
                // calculate the number of samples remaining until we ready for a block of audio, 
                // and wait that amount of time it will take to record.
                int remaining = bFirstBlock ? (midPoint - writePos) : (_recording.samples - writePos);
                float timeRemaining = (float)remaining / (float)_recordingHZ;

                yield return new WaitForSeconds(timeRemaining);
            }
        }
        yield break;
    }

    private void OnRecognize(SpeechRecognitionEvent result)
    {
        if (result != null && result.results.Length > 0)
        {
            foreach (var res in result.results)
            {
                foreach (var alt in res.alternatives)
                {
                    string text = string.Format("{0} ({1}, {2:0.00})\n", alt.transcript, res.final ? "Final" : "Interim", alt.confidence);
                    Debug.Log("ExampleStreaming.OnRecognize(): " + text);
                    //ResultsField.text = text;
                    if (alt.transcript.Contains("shield")) { voiceCommand = "4"; }
                    else if (alt.transcript.Contains("heal")) { voiceCommand = "5"; }
                    else if (alt.transcript.Contains("surrender")) { voiceCommand = "6"; }
                    else { voiceCommand = ""; }

                }

                if (res.keywords_result != null && res.keywords_result.keyword != null)
                {
                    foreach (var keyword in res.keywords_result.keyword)
                    {
                        Log.Debug("ExampleStreaming.OnRecognize()", "keyword: {0}, confidence: {1}, start time: {2}, end time: {3}", keyword.normalized_text, keyword.confidence, keyword.start_time, keyword.end_time);
                    }
                }

                if (res.word_alternatives != null)
                {
                    foreach (var wordAlternative in res.word_alternatives)
                    {
                        Log.Debug("ExampleStreaming.OnRecognize()", "Word alternatives found. Start time: {0} | EndTime: {1}", wordAlternative.start_time, wordAlternative.end_time);
                        foreach (var alternative in wordAlternative.alternatives)
                            Log.Debug("ExampleStreaming.OnRecognize()", "\t word: {0} | confidence: {1}", alternative.word, alternative.confidence);
                    }
                }
            }
        }
    }

    private void OnRecognizeSpeaker(SpeakerRecognitionEvent result)
    {
        if (result != null)
        {
            foreach (SpeakerLabelsResult labelResult in result.speaker_labels)
            {
                Log.Debug("ExampleStreaming.OnRecognizeSpeaker()", string.Format("speaker result: {0} | confidence: {3} | from: {1} | to: {2}", labelResult.speaker, labelResult.from, labelResult.to, labelResult.confidence));
            }
        }
    }
    #endregion
}
