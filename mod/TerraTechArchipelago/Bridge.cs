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
    // Everything the game thread touches is a queue. Unity is not thread-safe
    // and a socket callback that pokes a TankBlock from a worker thread is a
    // crash that only happens on other people's machines.
    internal sealed class Bridge : IDisposable
    {
        private const string Host = "127.0.0.1";
        private const int Port = 24601;

        private TcpClient _tcp;
        private StreamWriter _out;
        private Thread _reader;
        private volatile bool _running;
        private DateTime _nextDial = DateTime.MinValue;

        /// Lines received from the client, drained on the game thread.
        public readonly ConcurrentQueue<string> Inbox = new ConcurrentQueue<string>();

        public bool Connected => _tcp != null && _tcp.Connected;

        /// Called once per frame. Dials when disconnected, at most every few
        /// seconds — a tight reconnect loop would spam the log and the socket
        /// layer for as long as the client is closed, which is most of the time
        /// for a player who has not started it yet.
        public void Tick()
        {
            if (Connected || DateTime.UtcNow < _nextDial) return;
            _nextDial = DateTime.UtcNow.AddSeconds(3);
            TryConnect();
        }

        private void TryConnect()
        {
            try
            {
                Dispose();
                _tcp = new TcpClient();
                _tcp.Connect(Host, Port);
                var stream = _tcp.GetStream();
                _out = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };

                _running = true;
                _reader = new Thread(() => ReadLoop(stream)) { IsBackground = true };
                _reader.Start();

                Send("{\"cmd\":\"Hello\",\"mod_version\":\"" + Plugin.ModVersion +
                     "\",\"game_version\":\"" + Plugin.GameVersion + "\"}");
                Plugin.Log("Connected to the Archipelago client.");
            }
            catch (Exception)
            {
                // Silent: the client simply is not running yet. Saying so every
                // three seconds would bury the log the moment it matters.
                Dispose();
            }
        }

        private void ReadLoop(NetworkStream stream)
        {
            try
            {
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    while (_running)
                    {
                        string line = reader.ReadLine();
                        if (line == null) break;
                        if (line.Length > 0) Inbox.Enqueue(line);
                    }
                }
            }
            catch (Exception)
            {
                // A dropped connection is normal — the player closed the client.
            }
            finally
            {
                _running = false;
                Plugin.Log("Archipelago client disconnected.");
            }
        }

        public void Send(string json)
        {
            if (_out == null) return;
            try { _out.WriteLine(json); }
            catch (Exception) { Dispose(); }
        }

        public void Dispose()
        {
            _running = false;
            try { _out?.Dispose(); } catch { }
            try { _tcp?.Close(); } catch { }
            _out = null;
            _tcp = null;
        }
    }
}
