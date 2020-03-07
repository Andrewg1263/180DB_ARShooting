using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using uPLibrary.Networking.M2Mqtt.Utility;
using uPLibrary.Networking.M2Mqtt.Exceptions;
using System.Net;
using System.Net.Sockets;

public class Get_Info : MonoBehaviour
{
    [SerializeField] GameObject IPAddressInput;
    [SerializeField] GameObject StartGame;
    [SerializeField] GameObject Info;
    [SerializeField] GameObject SelectPlayer;
    Information Info_stored;
    MqttClient client;
    InputField UserInput;
    Button Scene_button;
    Dropdown selectPlayer;
    Text ButtonText;
    string player1_topic;
    string player2_topic;
    string Status;
    bool PlayerChosen;
    bool IPentered;
    bool Settingup;
    bool PlayerReady;
    List<string> player_list = new List<string>() { "Select Player","Player 1", "Player 2"};
    // Start is called before the first frame update
    void Start()
    {
        Info_stored = Info.GetComponent<Information>();
        selectPlayer = SelectPlayer.GetComponent<Dropdown>();
        player1_topic = "Player1";
        player2_topic = "Player2";
        PlayerChosen = false;
        IPentered = false;
        Settingup = true;
        PlayerReady = false;
        UserInput = IPAddressInput.GetComponent<InputField>();
        UserInput.onEndEdit.AddListener(Set_IPAddress);
        StartGame.SetActive(false);

        Scene_button = StartGame.GetComponent<Button>();
        ButtonText = StartGame.GetComponentInChildren<Text>();
        selectPlayer.AddOptions(player_list);
        selectPlayer.onValueChanged.AddListener(changeOnSelect);
    }
    void changeOnSelect(int index)
    {
        if (player_list[index] == "Player 1")
        {
            Set_Player1();
        }
        else if (player_list[index] == "Player 2")
        {
            Set_Player2();
        }

    }
    void LoadScene()
    {
        SceneManager.LoadScene("GamePlay", LoadSceneMode.Single);
    }

    void Set_Player2()
    {
        Info_stored.Set_PlayerNum(player2_topic);
        Info_stored.Set_EnemyNum(player1_topic);
        PlayerChosen = true;
    }

    void Set_Player1()
    {
        Info_stored.Set_PlayerNum(player1_topic);
        Info_stored.Set_EnemyNum(player2_topic);
        PlayerChosen = true;
    }

    void Set_IPAddress(string input)
    {
        try { IPAddress.Parse(input); }
        catch (FormatException e)
        {
            Debug.Log(e.Message);
            return;
        }
        Info_stored.Set_IP(input);
        IPentered = true;
    }

    void Update()
    {
        if (Settingup && IPentered && PlayerChosen)
        {
            try{ client = new MqttClient(IPAddress.Parse(Info_stored.Get_IP()), 1883, false, null); }
            catch(SocketException e)
            {
                Debug.Log(e.Message);
            }
            client.MqttMsgPublishReceived += client_MqttMsgPublishReceived;
            string clientId = Guid.NewGuid().ToString();
            try { client.Connect(clientId); }
            catch(SocketException e)
            {
                print(e.Message);
            }
            client.Subscribe(new string[] { "Status from " + Info_stored.Get_EnemyNum() }, new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE });
            client.Publish("Status from " + Info_stored.Get_PlayerNum(), System.Text.Encoding.UTF8.GetBytes(" Not Ready"), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, true);
            Settingup = false;
        }

        print("PC: " + PlayerChosen);
        print("IE: " + IPentered);
        print("PR: " + PlayerReady);
        print("Status: " + Status);
        if (PlayerChosen && IPentered && !PlayerReady)
        {
            StartGame.SetActive(true);
            ButtonText.text = "Ready";
            Scene_button.onClick.AddListener(TransmitReady);
        }

        if (PlayerReady && Status == "Ready")
        {
            ButtonText.text = "Start Game!";
            Scene_button.onClick.RemoveAllListeners();
            Scene_button.onClick.AddListener(LoadScene);
        }

    }

    void client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
    {
        Status = System.Text.Encoding.UTF8.GetString(e.Message);
    }

    void TransmitReady()
    {
        byte[] payload = System.Text.Encoding.UTF8.GetBytes("Ready");
        client.Publish("Status from "+Info_stored.Get_PlayerNum(), payload, MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, true);
        PlayerReady = true;
    }
}
