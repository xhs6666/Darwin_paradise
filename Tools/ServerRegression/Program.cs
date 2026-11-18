using System.Net;
using System.Net.Sockets;
using System.Text;
using DarwinPark.ServerCore;

int checks=0;
void Check(bool value,string name){if(!value)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
ServerCompatibility Release(string patch="0.1.0")=>new(){protocol=2,contentEpoch=1,minimumSave=1,maximumSave=1,release=patch};
var release=Release();release.Validate();
Check(release.Reject(Release("0.1.1"))==null,"compatible patch release");
Check(release.Reject(null)!=null,"missing handshake rejected");
var remote=Release();remote.protocol=1;Check(release.Reject(remote)!=null,"old protocol rejected");
remote=Release();remote.contentEpoch=2;Check(release.Reject(remote)!=null,"different content epoch rejected");
remote=Release();remote.minimumSave=2;remote.maximumSave=2;Check(release.Reject(remote)!=null,"future save range rejected");
bool failed=false;try{release.RequireSave(2);}catch(InvalidOperationException){failed=true;}Check(failed,"future world rejected before mutation");
var admission=new ServerAdmission(0);
Check(!admission.CanCommand&&!admission.AcknowledgeWorld(0),"ready/command before join rejected");
Check(admission.Expired(13,12,180),"unjoined timeout");
Check(admission.Join(release,Release(),14)==null&&!admission.CanCommand,"join cannot command before world ready");
Check(!admission.AcknowledgeWorld(0),"ready before world is sent rejected");
admission.Loading(14);Check(admission.AcknowledgeWorld(1)&&admission.CanCommand,"world ready enables commands");
admission.Loading(20);Check(!admission.CanCommand&&admission.Expired(201,12,180),"world reload resets permission and times out");
Check(!admission.AcknowledgeWorld(1)&&!admission.CanCommand,"old ready cannot acknowledge new world");
Check(admission.Join(release,Release(),30)!=null,"duplicate join rejected");
var rejected=new ServerAdmission(0);rejected.Join(release,null,0);Check(rejected.Rejected&&!rejected.AcknowledgeWorld(0),"rejected peer cannot ready");
admission.AcknowledgeWorld(2);admission.Block();Check(!admission.CanCommand&&!admission.AcknowledgeWorld(2),"blocked connection cannot resume command execution");
remote=Release();remote.minimumSave=0;Check(release.Reject(remote)!=null,"invalid remote save range rejected");
failed=false;try{release.RequireWorld(1,0);}catch(InvalidOperationException){failed=true;}Check(failed,"world without content marker rejected without migration");
failed=false;try{release.RequireWorld(1,2);}catch(InvalidOperationException){failed=true;}Check(failed,"content downgrade requires explicit migration");

// Real loopback socket exercises the exact production framing and close-after-rejection path.
var accepted=new TaskCompletionSource<TcpClient>(TaskCreationOptions.RunContinuationsAsynchronously);
using var listener=new LanListener(0,c=>accepted.TrySetResult(c));
using var client=new TcpClient();await client.ConnectAsync(IPAddress.Loopback,listener.Port);
using var socket=await accepted.Task.WaitAsync(TimeSpan.FromSeconds(5));
var received=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
var closed=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
using var connection=new LanConnection(socket,1024,8,(_,json)=>received.TrySetResult(json),_=>closed.TrySetResult(true));connection.Start();
byte[] data=Encoding.UTF8.GetBytes("{\"type\":\"join\",\"text\":\"中文\"}");
var stream=client.GetStream();await stream.WriteAsync(BitConverter.GetBytes(data.Length));
for(int i=0;i<data.Length;i++)await stream.WriteAsync(data.AsMemory(i,1));
Check(await received.Task.WaitAsync(TimeSpan.FromSeconds(5))==Encoding.UTF8.GetString(data),"fragmented UTF8 framed input");
connection.SendAndClose("version mismatch");
byte[] header=new byte[4];await stream.ReadExactlyAsync(header);byte[] response=new byte[BitConverter.ToInt32(header)];await stream.ReadExactlyAsync(response);
Check(Encoding.UTF8.GetString(response)=="version mismatch","rejection delivered before closing");
await closed.Task.WaitAsync(TimeSpan.FromSeconds(5));Check(!connection.Connected,"close callback and state");

// Malformed frame length closes instead of allocating attacker-controlled memory.
var second=new TaskCompletionSource<TcpClient>(TaskCreationOptions.RunContinuationsAsynchronously);
using var listener2=new LanListener(0,c=>second.TrySetResult(c));using var client2=new TcpClient();await client2.ConnectAsync(IPAddress.Loopback,listener2.Port);
var badClosed=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
using var bad=new LanConnection(await second.Task,1024,8,(_,_)=>throw new Exception("invalid frame delivered"),_=>badClosed.TrySetResult(true));bad.Start();
await client2.GetStream().WriteAsync(BitConverter.GetBytes(int.MaxValue));await badClosed.Task.WaitAsync(TimeSpan.FromSeconds(5));Check(!bad.Connected,"oversized frame rejected");
Console.WriteLine($"{checks} checks passed");
