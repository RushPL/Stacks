﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Tcp
{
    public class MessageClient : IMessageClient
    {
        private IFramedClient framedClient;
        private StacksSerializationHandler packetSerializer;

        public IExecutor Executor
        {
            get { return framedClient.Executor; }
        }

        public bool IsConnected
        {
            get { return framedClient.IsConnected; }
        }

        public event Action Connected
        {
            add { this.framedClient.Connected += value; }
            remove { this.framedClient.Connected -= value; }
        }

        public event Action<Exception> Disconnected
        {
            add { this.framedClient.Disconnected += value; }
            remove { this.framedClient.Disconnected -= value; }
        }
        public event Action<int> Sent
        {
            add { this.framedClient.Sent += value; }
            remove { this.framedClient.Sent -= value; }
        }

        public MessageClient(IFramedClient framedClient, 
                             IStacksSerializer packetSerializer,
                             IMessageHandler messageHandler)
        {
            this.framedClient = framedClient;
            this.packetSerializer = new StacksSerializationHandler(this, packetSerializer, messageHandler);

            this.framedClient.Received += PacketReceived;
        }

        public Task Connect(IPEndPoint endPoint)
        {
            return framedClient.Connect(endPoint);
        }

        private unsafe void PacketReceived(ArraySegment<byte> buffer)
        {
            fixed (byte* b = &buffer.Array[buffer.Offset])
            {
                int typeCode = *((int*)b);
                using (var ms = new MemoryStream(buffer.Array, buffer.Offset + 4, buffer.Count - 4))
                {
                    this.packetSerializer.Deserialize(typeCode, ms);
                }
            }
        }

        public unsafe void Send<T>(int typeCode, T obj)
        {
            using (var ms = new MemoryStream())
            {
                ms.SetLength(4);
                ms.Position = 4;
                this.packetSerializer.Serialize(obj, ms);
                ms.Position = 0;
                
                var buffer = ms.GetBuffer();

                fixed (byte* buf = buffer)
                {
                    int* iBuf = (int*)buf;
                    *iBuf = typeCode;
                }

                this.framedClient.SendPacket(new ArraySegment<byte>(buffer, 0, (int)ms.Length));
            }
        }

        public void Close()
        {
            this.framedClient.Close();
        }

    }
}
