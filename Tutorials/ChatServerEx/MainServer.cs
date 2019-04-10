﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Runtime.CompilerServices;

using SuperSocket.SocketBase;
using SuperSocket.SocketBase.Protocol;
using SuperSocket.SocketEngine;

using CSBaseLib;
using DB;


//TODO 1. 주기적으로 접속한 세션이 패킷을 주고 받았는지 조사(좀비 클라이언트 검사)

namespace ChatServer
{
    public class MainServer : AppServer<ClientSession, EFBinaryRequestInfo>
    {
        public static ChatServerOption ServerOption;
        public static SuperSocket.SocketBase.Logging.ILog MainLogger;

        SuperSocket.SocketBase.Config.IServerConfig m_Config;        
        static RemoteConnectCheck RemoteCheck = null;
        PacketDistributor Distributor = new PacketDistributor();

        
        public MainServer()
            : base(new DefaultReceiveFilterFactory<ReceiveFilter, EFBinaryRequestInfo>())
        {
            NewSessionConnected += new SessionHandler<ClientSession>(OnConnected);
            SessionClosed += new SessionHandler<ClientSession, CloseReason>(OnClosed);
            NewRequestReceived += new RequestHandler<ClientSession, EFBinaryRequestInfo>(OnPacketReceived);
        }

        public void InitConfig(ChatServerOption option)
        {
            ServerOption = option;

            m_Config = new SuperSocket.SocketBase.Config.ServerConfig
            {
                Name = option.Name,
                Ip = "Any",
                Port = option.Port,
                Mode = SocketMode.Tcp,
                MaxConnectionNumber = option.MaxConnectionNumber,
                MaxRequestLength = option.MaxRequestLength,
                ReceiveBufferSize = option.ReceiveBufferSize,
                SendBufferSize = option.SendBufferSize
            };
        }

        public void CreateStartServer()
        {
            try
            {
                bool bResult = Setup(new SuperSocket.SocketBase.Config.RootConfig(), m_Config, logFactory: new SuperSocket.SocketBase.Logging.NLogLogFactory());

                if (bResult == false)
                {
                    Console.WriteLine("[ERROR] 서버 네트워크 설정 실패 ㅠㅠ");
                    return;
                }
                else
                {
                    MainLogger = base.Logger;
                    MainLogger.Info("서버 초기화 성공");
                }

                Start();

                StartRemoteConnect();

                ClientSession.CreateIndexPool(m_Config.MaxConnectionNumber);

                MainLogger.Info("서버 생성 성공");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 서버 생성 실패: {ex.ToString()}");
            }


            //ActiveServerBootstrap = BootstrapFactory.CreateBootstrap();

            //if (!ActiveServerBootstrap.Initialize())
            //{
            //    Console.WriteLine(string.Format("서버 초기화 실패"), LOG_LEVEL.ERROR);
            //    return;
            //}
            //else
            //{
            //    var refAppServer = ActiveServerBootstrap.AppServers.FirstOrDefault() as MainServer;
            //    MainLogger = refAppServer.Logger;
            //    WriteLog("서버 초기화 성공", LOG_LEVEL.INFO);
            //}


            //var result = ActiveServerBootstrap.Start();

            //if (result != StartResult.Success)
            //{
            //    MainServer.WriteLog(string.Format("서버 시작 실패"), LOG_LEVEL.ERROR);
            //    return;
            //}
            //else
            //{
            //    WriteLog("서버 시작 성공", LOG_LEVEL.INFO);
            //}

            //WriteLog(string.Format("서버 생성 및 시작 성공"), LOG_LEVEL.INFO);

            
            //ChatServerEnvironment.Setting();
                        
            //StartRemoteConnect();

            //var appServer = ActiveServerBootstrap.AppServers.FirstOrDefault() as MainServer;
            //InnerMessageHostProgram.ServerStart(ChatServerEnvironment.ChatServerUniqueID, appServer.Config.Port);

            //ClientSession.CreateIndexPool(appServer.Config.MaxConnectionNumber);            
        }

        public void StartRemoteConnect()
        {
            RemoteCheck = new RemoteConnectCheck();

            var remoteInfoList = new List<Tuple<string, string, int>>();

            foreach(var server in ConfigTemp.RemoteServers)
            {
                var infoList = server.Split(":");
                remoteInfoList.Add(new Tuple<string, string, int>(infoList[0], infoList[1], infoList[2].ToInt32()));

                MainLogger.Info(string.Format("(To)연결할 서버 정보: {0}, {1}, {2}", infoList[0], infoList[1], infoList[2]));
            }

            RemoteCheck.Init(this, remoteInfoList);
        }

        public void StopServer()
        {            
            RemoteCheck.Stop();

            Stop();

            Distributor.Destory();
        }

        public ERROR_CODE CreateComponent()
        {
            var error = Distributor.Create(this);

            if (error != ERROR_CODE.NONE)
            {
                return error;
            }

            MainLogger.Info("CreateComponent - Success");
            return ERROR_CODE.NONE;
        }

        //TODO TimeOut을 3초로 잡고, 상대방이 3초동안 receive를 하지 않아도 send에 문제가 없는지 알아본다.
        public bool SendData(string sessionID, byte[] sendData)
        {
            try
            {
                var session = GetSessionByID(sessionID);

                if (session == null)
                {
                    return false;
                }

                session.Send(sendData, 0, sendData.Length);
            }
            catch(Exception)
            {
                //TODO send time out 등의 문제이므로 접속을 끊는 것이 좋다.
                //session.SendEndWhenSendingTimeOut(); 
                //session.Close();
            }
            return true;
        }

        public PacketDistributor GetPacketDistributor() { return Distributor; }
                
        void OnConnected(ClientSession session)
        {
            //옵션의 최대 연결 수를 넘으면 SuperSocket이 바로 접속을 짤라버린다. 즉 이 OnConneted 함수가 호출되지 않는다

            session.AllocSessionIndex();
            MainLogger.Info(string.Format("세션 번호 {0} 접속", session.SessionID));
                        
            var packet = ServerPacketData.MakeNTFInConnectOrDisConnectClientPacket(true, session.SessionID, session.SessionIndex);            
            Distributor.DistributeCommon(false, packet);
        }

        void OnClosed(ClientSession session, CloseReason reason)
        {
            MainLogger.Info(string.Format("세션 번호 {0} 접속해제: {1}", session.SessionID, reason.ToString()));


            var packet = ServerPacketData.MakeNTFInConnectOrDisConnectClientPacket(false, session.SessionID, session.SessionIndex);
            Distributor.DistributeCommon(false, packet);

            session.FreeSessionIndex(session.SessionIndex);
        }

        void OnPacketReceived(ClientSession session, EFBinaryRequestInfo reqInfo)
        {
            MainLogger.Debug(string.Format("세션 번호 {0} 받은 데이터 크기: {1}, ThreadId: {2}", session.SessionID, reqInfo.Body.Length, System.Threading.Thread.CurrentThread.ManagedThreadId));

            var packet = new ServerPacketData();
            packet.SessionID = session.SessionID;
            packet.SessionIndex = session.SessionIndex;
            packet.PacketSize = reqInfo.Size;            
            packet.PacketID = reqInfo.PacketID;
            packet.Type = reqInfo.Type;
            packet.BodyData = reqInfo.Body;
                    
            Distributor.Distribute(packet);
        }
    }

    class ConfigTemp
    {
        static public List<string> RemoteServers = new List<string>();
    }
}
