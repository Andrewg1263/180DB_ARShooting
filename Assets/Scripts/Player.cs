// record the ammo, health and display text accrodingly
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class Player : MonoBehaviour
{
    [SerializeField] AudioClip reloadSound;
    [SerializeField] AudioClip anti_bioticSound;
    [SerializeField] AudioClip shieldSound;
    [SerializeField] AudioClip damageSound;
    [SerializeField] AudioClip fireSound;
    [SerializeField] AudioClip chargedShotSound;
    [SerializeField] AudioClip outOfAmmoSound;
    [SerializeField] int m_ammo;
    [SerializeField] int m_health;
    [SerializeField] int m_shield;
    AudioSource m_AudioSource;
    bool canUseAntiobiotics = true;
    bool canUseShield = true;

    void Awake()
    {
        m_ammo = 10;
        m_health = 10;
        m_shield = 0;
        m_AudioSource = GetComponent<AudioSource>();
    }

    public void Reload()
    {
        if(m_ammo < 10)
        {
            m_ammo = Mathf.Min(10, m_ammo + 3);
            PlaySound("reload");
        }
    }

    public int ReduceHealthBy(int d)
    {   
        if(m_shield - d < 0)
        {
            m_health -= d - m_shield;
            if(m_health < 0){ m_health = 0; }
        }
        else if(m_shield - d == 0)
        {
            m_shield = 0;
        }
        else
        {
            m_shield -= d;
        }
        PlaySound("damage");
        return m_health;
    }

    public void ActivateShield()
    {
        if (!canUseShield)
            return;
        m_shield = 3;
        PlaySound("shield");
        canUseShield = false;
    }

    public void UseAntibiotics()
    {
        if (!canUseAntiobiotics)
            return; 
        m_health += 3;
        PlaySound("antibiotics");
        canUseAntiobiotics = false;

    }

    public void ReduceAmmoBy(int d)
    {
        PlayGunSound(d);
        m_ammo -= d;
    }

    public int Get_Ammo()
    {
        return m_ammo;
    }

    public int Get_Health()
    {
        return m_health;
    }

    public int Get_Shield()
    {
        return m_shield;
    }

    public bool CheckReq(int scale)
    {
        if (scale <= m_ammo)
            return true;
        else
        {
            Debug.Log("NO AMMO!");
            PlaySound("no ammo");
            return false;
        }
    }


    public void PlaySound(string action)
    {
        switch(action)
        {
            case "shield":
            {
                m_AudioSource.clip = shieldSound;
                break;
            }
            case "damage":
            {
                m_AudioSource.clip = damageSound;
                break;
            }
            case "reload":
            {
                m_AudioSource.clip = reloadSound;
                break;
            }
            case "antibiotics":
            {
                m_AudioSource.clip = anti_bioticSound;
                break;
            }
            case "no ammo":

            {
                m_AudioSource.clip = outOfAmmoSound;
                break;
            }
            default:
            {
                break;
            }

        }
        m_AudioSource.Play();
    }

    public void PlayGunSound(int scale)
    {
        if (scale == 1)
        {
            m_AudioSource.clip = fireSound;
        }
        else
        {
            m_AudioSource.clip = chargedShotSound;
        }
        m_AudioSource.Play();
    }


    public void Reset()
    {
        m_ammo = 10;
        m_health = 10;
        m_shield = 0;
    }
}
