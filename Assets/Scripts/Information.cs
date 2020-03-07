using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Information : MonoBehaviour
{
    static string player_num;
    static string enemy_num;
    static string IP_Address;

    public string Get_EnemyNum()
    {
        return enemy_num;
    }
    public void Set_EnemyNum(string input)
    {
        enemy_num = input;
    }
    public string Get_PlayerNum()
    {
        return player_num;
    }
    public void Set_PlayerNum(string input)
    {
        player_num = input;
    }
    public string Get_IP()
    {
        return IP_Address;
    }
    public void Set_IP(string input)
    {
        IP_Address = input;
    }
}
