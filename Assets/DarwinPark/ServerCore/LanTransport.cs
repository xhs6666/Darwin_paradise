using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DarwinPark.ServerCore
{
    // ★ 新增：TCP帧只在后台读写，Unity对象只在主线程处理；长度/排队上限防止大包或慢成员阻塞游戏。
    public sealed class LanConnection : IDisposable
    {
        private readonly TcpClient _client;private readonly int _maxBytes,_maxQueue;
        private readonly ConcurrentQueue<(string json,bool close)> _outgoing=new ConcurrentQueue<(string,bool)>();
        private readonly SemaphoreSlim _signal=new SemaphoreSlim(0);private readonly Action<LanConnection,string> _received;private readonly Action<LanConnection> _closed;
        private int _disposed,_closing;
        public string Id {get;}=Guid.NewGuid().ToString("N");
        public bool Connected=>Volatile.Read(ref _disposed)==0;
        public LanConnection(TcpClient client,int maxBytes,int maxQueue,Action<LanConnection,string> received,Action<LanConnection> closed)
        {_client=client;_client.NoDelay=true;_maxBytes=maxBytes;_maxQueue=maxQueue;_received=received;_closed=closed;}
        public void Start(){_ = Task.Run(ReadLoop);_ = Task.Run(WriteLoop);}
        public void Send(string json)
        {
            if(!Connected||Volatile.Read(ref _closing)!=0)return;Enqueue(json,false);
        }
        // ✦ 新增：拒绝原因写完再断开，避免客户端只看到不明确的网络中断。
        public void SendAndClose(string json)
        {if(!Connected||Interlocked.Exchange(ref _closing,1)!=0)return;Enqueue(json,true);}
        private void Enqueue(string json,bool close)
        {if(_outgoing.Count>=_maxQueue){Dispose();return;}_outgoing.Enqueue((json,close));_signal.Release();}
        private async Task ReadLoop()
        {
            try
            {
                var stream=_client.GetStream();var header=new byte[4];
                while(Connected)
                {
                    await Exact(stream,header);int length=header[0]|header[1]<<8|header[2]<<16|header[3]<<24;
                    if(length<=0||length>_maxBytes)throw new InvalidDataException("网络数据包长度无效");
                    var bytes=new byte[length];await Exact(stream,bytes);_received(this,Encoding.UTF8.GetString(bytes));
                }
            }
            catch(Exception ex) when(ex is IOException||ex is SocketException||ex is ObjectDisposedException||ex is InvalidOperationException){ }
            finally{Dispose();}
        }
        private static async Task Exact(NetworkStream stream,byte[] bytes)
        {int offset=0;while(offset<bytes.Length){int count=await stream.ReadAsync(bytes,offset,bytes.Length-offset);if(count==0)throw new EndOfStreamException();offset+=count;}}
        private async Task WriteLoop()
        {
            try
            {
                var stream=_client.GetStream();
                while(Connected)
                {
                    await _signal.WaitAsync();if(!Connected)break;
                    if(!_outgoing.TryDequeue(out var packet))continue;var bytes=Encoding.UTF8.GetBytes(packet.json);
                    if(bytes.Length>_maxBytes)throw new InvalidDataException("网络快照超出配置上限");
                    int length=bytes.Length;var header=new[]{(byte)length,(byte)(length>>8),(byte)(length>>16),(byte)(length>>24)};
                    await stream.WriteAsync(header,0,4);await stream.WriteAsync(bytes,0,bytes.Length);
                    if(packet.close)break;
                }
            }
            catch(Exception ex) when(ex is IOException||ex is SocketException||ex is ObjectDisposedException||ex is InvalidOperationException){ }
            finally{Dispose();}
        }
        public void Dispose(){if(Interlocked.Exchange(ref _disposed,1)!=0)return;_client.Close();_signal.Release();_closed(this);}
    }
    public sealed class LanListener : IDisposable
    {
        private readonly TcpListener _listener;private volatile bool _stopped;
        public int Port=>((IPEndPoint)_listener.LocalEndpoint).Port;
        public LanListener(int port,Action<TcpClient> accept)
        {
            _listener=new TcpListener(IPAddress.Any,port);_listener.Start();
            _=Task.Run(async()=>{while(!_stopped)try{var client=await _listener.AcceptTcpClientAsync();if(_stopped)client.Close();else accept(client);}catch(Exception ex)when(ex is SocketException||ex is ObjectDisposedException){break;}});
        }
        public void Dispose(){_stopped=true;_listener.Stop();}
    }
}
