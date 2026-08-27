using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace TerraTechArchipelago
{
    // Bridge — the mod's end of the line to the Archipelago client.
    //
    // One JSON object per line, both directions. The client listens and we
    // dial it, because the game starts and stops far more often than the
    // client does; a listener that outlives the game needs no reconnect
    // guessing on the side that restarts.
    //
    // ⚠⚠ THE GAME THREAD NEVER TOUCHES THE SOCKET. Not to write, not to
    // connect, not to close. It only ever puts a string in a queue or takes
    // one out.
    //
    // This is not tidiness. An earlier version wrote straight from the game
    // thread and dialled from Update(), and both of those block: a TCP write
    // waits when the peer has stopped reading, and Connect waits on the
    // operating system. When the launcher went away mid-session, the first
    // check the player earned froze the entire game — the worst possible
    // moment, because it happens exactly when something good just happened.
    internal sealed class Bridge : IDisposable
    {
        private const string Host = "127.0.0.1";
        private const int Port = 24601;

        /// Test seam. The proof harness runs while a REAL session may be
        /// holding the real port on this machine, so it points the bridge at
        /// a scratch port instead. Zero everywhere outside the harness.
        internal static int PortOverride;

        private static int ActivePort => PortOverride != 0 ? PortOverride : Port;

        /// How long to wait on a connect before giving up and trying later.
        /// Loopback answers in microseconds or not at all, so this only ever
        /// matters when something is half-listening.
        private const int ConnectTimeoutMs = 1500;
        private const int RedialDelayMs = 3000;

        private readonly ConcurrentQueue<string> _outbox = new ConcurrentQueue<string>();

        /// Lines received from the client, drained on the game thread.
        public readonly ConcurrentQueue<string> Inbox = new ConcurrentQueue<string>();

        private Thread _worker;
        private volatile bool _running;
        private volatile bool _connected;

        public bool Connected => _connected;

        /// Start the one thread that owns the socket for the mod's lifetime.
        public void Start()
        {
            if (_worker != null) return;
            _running = true;
            _worker = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = "ArchipelagoBridge",
            };
            _worker.Start();
        }

        /// Called once per frame. Deliberately does nothing but make sure the
        /// worker exists: everything that can block lives on that thread.
        public void Tick()
        {
            if (_worker == null) Start();
        }

        /// Queue a line. Never blocks, never throws, never touches the socket.
        public void Send(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            // A cap, so a session played with no launcher attached cannot grow
            // the queue without limit. Checks are re-sent on reconnect anyway
            // — the mod replays from its own slot file — so dropping the
            // oldest costs nothing that cannot be recovered.
            while (_outbox.Count > 4096 && _outbox.TryDequeue(out _)) { }
            _outbox.Enqueue(json);
        }

        // --- everything below runs on the worker thread ---------------------

        private void WorkerLoop()
        {
            while (_running)
            {
                TcpClient tcp = null;
                try
                {
                    tcp = TryConnect();
                    if (tcp == null)
                    {
                        Sleep(RedialDelayMs);
                        continue;
                    }

                    NetworkStream stream = tcp.GetStream();
                    var reader = new StreamReader(stream, Encoding.UTF8);
                    var writer = new StreamWriter(stream, new UTF8Encoding(false))
                    { AutoFlush = true };

                    _connected = true;
                    Plugin.Log("Connected to the Archipelago client.");

                    // The reader blocks; that is fine on this thread. Give it
                    // its own so the writer is never held up behind it.
                    var readerThread = new Thread(() => ReadLoop(reader))
                    { IsBackground = true, Name = "ArchipelagoBridgeRead" };
                    readerThread.Start();

                    Send("{\"cmd\":\"Hello\",\"mod_version\":\"" + Plugin.ModVersion +
                         "\",\"game_version\":\"" + Plugin.GameVersion + "\"}");

                    WriteLoop(writer, tcp);
                }
                catch (Exception)
                {
                    // Any failure here is a dropped connection, which is
                    // normal: the player closed the launcher.
                }
                finally
                {
                    _connected = false;
                    try { tcp?.Close(); } catch { }
                }

                if (_running)
                {
                    Plugin.Log("Archipelago client disconnected — will keep trying "
                             + "to reconnect.");
                    Sleep(RedialDelayMs);
                }
            }
        }

        /// Connect with a deadline. TcpClient.Connect can sit for the OS's own
        /// timeout, which is far longer than a player will wait patiently.
        private TcpClient TryConnect()
        {
            var tcp = new TcpClient();
            try
            {
                IAsyncResult ar = tcp.BeginConnect(Host, ActivePort, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(ConnectTimeoutMs, false))
                {
                    tcp.Close();
                    return null;
                }
                tcp.EndConnect(ar);
                return tcp.Connected ? tcp : null;
            }
            catch (Exception)
            {
                // Silent: the client simply is not running yet. Saying so
                // every three seconds would bury the log the moment it matters.
                try { tcp.Close(); } catch { }
                return null;
            }
        }

        private void ReadLoop(StreamReader reader)
        {
            try
            {
                while (_running && _connected)
                {
                    string line = reader.ReadLine();
                    if (line == null) break;
                    if (line.Length > 0) Inbox.Enqueue(line);
                }
            }
            catch (Exception) { /* the write loop notices and reconnects */ }
            finally { _connected = false; }
        }

        private void WriteLoop(StreamWriter writer, TcpClient tcp)
        {
            while (_running && _connected)
            {
                string line;
                if (_outbox.TryDequeue(out line))
                {
                    try { writer.WriteLine(line); }
                    catch (Exception)
                    {
                        // Put it back: a line lost here is a check the server
                        // never hears about.
                        _outbox.Enqueue(line);
                        _connected = false;
                        return;
                    }
                    continue;
                }

                // Nothing to send. Notice a dead peer rather than spinning.
                if (!IsStillAlive(tcp)) { _connected = false; return; }
                Sleep(25);
            }
        }

        /// A socket the peer closed reports Connected = true until something
        /// touches it. Poll for a readable-but-empty socket, which is what a
        /// clean close looks like.
        private static bool IsStillAlive(TcpClient tcp)
        {
            try
            {
                Socket s = tcp.Client;
                if (s == null) return false;
                bool readable = s.Poll(0, SelectMode.SelectRead);
                return !(readable && s.Available == 0);
            }
            catch { return false; }
        }

        private static void Sleep(int ms)
        {
            try { Thread.Sleep(ms); } catch { }
        }

        public void Dispose()
        {
            _running = false;
            _connected = false;
            _worker = null;
        }
    }
}
