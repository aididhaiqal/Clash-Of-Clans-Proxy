﻿using System.Net.Sockets;

namespace ClashOfClansProxy
{
    public class State
    {
        public Socket socket;
        public const int BufferSize = 1024;
        public byte[] buffer = new byte[BufferSize];
        public byte[] packet = new byte[0];
    }
}
