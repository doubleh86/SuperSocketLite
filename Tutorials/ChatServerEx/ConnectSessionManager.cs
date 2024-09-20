﻿using System.Collections.Generic;


namespace ChatServer;

// 전체 연결된 세션의 상태 관리
public class ConnectSessionManager
{        
    List<ConnectSession> _sessionList = new List<ConnectSession>();
    ConnectSession _disableSession = new ConnectSession();


    public void CreateSession(int maxCount)
    {
        for (int i = 0; i < maxCount; ++i)
        {
            _sessionList.Add(new ConnectSession());
        }


        _disableSession.IsEnable = false;
    }

    public void SetClear(int index)
    {
        var session = GetSession(index);
        session.Clear();
    }

    public void SetDisable(int index)
    {
        var session = GetSession(index);
        session.SetDisable();
    }

    public int GetRoomNumber(int index)
    {
        var session = GetSession(index);
        return session.GetRoomNumber();
    }

    public bool EnableReuqestLogin(int index)
    {
        var session = GetSession(index);
        return session.IsStateNone();
    }

    public bool IsStateRoom(int index)
    {
        var session = GetSession(index);
        return session.IsStateRoom();
    }

    public void SetPreLogin(int index)
    {
        var session = GetSession(index);
        session.SetStatePreLogin();
    }

    public void SetLogin(int index, string userID)
    {
        var session = GetSession(index);
        session.SetStateLogin(userID);
    }

    public void SetStateNone(int index)
    {
        var session = GetSession(index);
        session.SetStateNone();
    }

    public void SetStateLogin(int index)
    {
        var session = GetSession(index);
        session.SetStateLogin();
    }

    public bool EnableReuqestEnterRoom(int index)
    {
        var session = GetSession(index);
        return session.IsStateLogin();
    }

    public bool SetPreRoomEnter(int index, int roomNumber)
    {
        var session = GetSession(index);
        return session.SetPreRoomEnter(roomNumber);
    }

    public bool SetRoomEntered(int index, int roomNumber)
    {
        var session = GetSession(index);
        return session.SetRoomEntered(roomNumber);
    }


    ConnectSession GetSession(int index)
    {
        if(0 <= index && index < ClientSession.s_MaxSessionCount)
        {
            return _sessionList[index];
        }

        return _disableSession;
    }
    
}
