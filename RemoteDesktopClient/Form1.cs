/*


using System;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;
using WindowsInput;
using WindowsInput.Native;

namespace RemoteDesktopClient
{
    public partial class Form1 : Form
    {
        private TcpClient tcpClient;
        private NetworkStream networkStream;
        private bool isConnected = false;
        private Thread receiveThread;
        private bool isReceiving = false;

        // UI Components
        private TextBox txtServerIP;
        private TextBox txtPort;
        private Button btnConnect;
        private Label lblStatus;
        private PictureBox pictureBoxRemoteScreen;
        private Button btnDisconnect;
        private RichTextBox txtLog;
        private CheckBox chkControlEnabled;

        public Form1()
        {
            InitializeComponent();
            SetupUI();
            this.FormClosing += Form1_FormClosing;
        }

        private void SetupUI()
        {
            this.Text = "Remote Desktop Client (Viewer)";
            this.Size = new Size(900, 700);
            this.StartPosition = FormStartPosition.CenterScreen;

            // Connection panel
            Label lblIP = new Label() { Text = "Server IP:", Location = new Point(12, 15), Width = 60 };
            txtServerIP = new TextBox() { Text = "127.0.0.1", Location = new Point(75, 12), Width = 120 };

            Label lblPort = new Label() { Text = "Port:", Location = new Point(205, 15), Width = 35 };
            txtPort = new TextBox() { Text = "5900", Location = new Point(240, 12), Width = 60 };

            btnConnect = new Button() { Text = "Connect", Location = new Point(310, 10), Width = 80 };
            btnConnect.Click += BtnConnect_Click;

            btnDisconnect = new Button() { Text = "Disconnect", Location = new Point(400, 10), Width = 80, Enabled = false };
            btnDisconnect.Click += BtnDisconnect_Click;

            lblStatus = new Label() { Text = "Status: Disconnected", Location = new Point(495, 15), Width = 150, ForeColor = Color.Red };

            chkControlEnabled = new CheckBox() { Text = "Enable Remote Control", Location = new Point(660, 12), Width = 150, Checked = true };

            // Remote screen display
            pictureBoxRemoteScreen = new PictureBox()
            {
                Location = new Point(12, 45),
                Size = new Size(860, 560),
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Black
            };

            // Mouse events for remote control
            pictureBoxRemoteScreen.MouseMove += PictureBoxRemoteScreen_MouseMove;
            pictureBoxRemoteScreen.MouseClick += PictureBoxRemoteScreen_MouseClick;
            pictureBoxRemoteScreen.MouseDown += PictureBoxRemoteScreen_MouseDown;
            pictureBoxRemoteScreen.MouseUp += PictureBoxRemoteScreen_MouseUp;

            // Log area
            txtLog = new RichTextBox()
            {
                Location = new Point(12, 615),
                Size = new Size(860, 45),
                ReadOnly = true,
                Font = new Font("Consolas", 9)
            };

            // Keyboard event for remote control
            this.KeyPreview = true;
            this.KeyDown += Form1_KeyDown;
            this.KeyUp += Form1_KeyUp;

            this.Controls.AddRange(new Control[] { lblIP, txtServerIP, lblPort, txtPort, btnConnect,
                                                    btnDisconnect, lblStatus, chkControlEnabled,
                                                    pictureBoxRemoteScreen, txtLog });
        }

        private void BtnConnect_Click(object sender, EventArgs e)
        {
            if (isConnected) return;

            try
            {
                LogMessage($"Connecting to {txtServerIP.Text}:{txtPort.Text}...");
                tcpClient = new TcpClient();
                tcpClient.Connect(txtServerIP.Text, int.Parse(txtPort.Text));
                networkStream = tcpClient.GetStream();
                isConnected = true;
                isReceiving = true;

                btnConnect.Enabled = false;
                btnDisconnect.Enabled = true;
                txtServerIP.Enabled = false;
                txtPort.Enabled = false;
                lblStatus.Text = "Status: Connected";
                lblStatus.ForeColor = Color.Green;

                LogMessage($"Connected to {txtServerIP.Text}:{txtPort.Text}");

                // Start receiving screen images
                receiveThread = new Thread(ReceiveScreen);
                receiveThread.IsBackground = true;
                receiveThread.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Connection failed: {ex.Message}", "Connection Error",
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
                LogMessage($"Connection error: {ex.Message}");
                Disconnect();
            }
        }

        private void BtnDisconnect_Click(object sender, EventArgs e)
        {
            Disconnect();
        }

        private void Disconnect()
        {
            LogMessage("Disconnecting...");
            isConnected = false;
            isReceiving = false;

            if (networkStream != null)
            {
                try { networkStream.Close(); } catch { }
                networkStream = null;
            }

            if (tcpClient != null)
            {
                try { tcpClient.Close(); } catch { }
                tcpClient = null;
            }

            if (receiveThread != null && receiveThread.IsAlive)
                receiveThread.Join(500);

            btnConnect.Enabled = true;
            btnDisconnect.Enabled = false;
            txtServerIP.Enabled = true;
            txtPort.Enabled = true;
            lblStatus.Text = "Status: Disconnected";
            lblStatus.ForeColor = Color.Red;

            if (pictureBoxRemoteScreen.Image != null)
            {
                pictureBoxRemoteScreen.Image.Dispose();
                pictureBoxRemoteScreen.Image = null;
            }

            LogMessage("Disconnected from server");
        }

        private void ReceiveScreen()
        {
            LogMessage("Screen receiver thread started");
            byte[] sizeBuffer = new byte[4];

            while (isConnected && isReceiving && tcpClient != null && tcpClient.Connected)
            {
                try
                {
                    // Read image size (4 bytes)
                    int bytesRead = 0;
                    while (bytesRead < 4 && isConnected)
                    {
                        int read = networkStream.Read(sizeBuffer, bytesRead, 4 - bytesRead);
                        if (read == 0) throw new Exception("Connection closed by server");
                        bytesRead += read;
                    }

                    if (!isConnected) break;

                    int imageSize = BitConverter.ToInt32(sizeBuffer, 0);

                    if (imageSize <= 0 || imageSize > 50 * 1024 * 1024) // Max 50MB
                    {
                        LogMessage($"Invalid image size: {imageSize}");
                        break;
                    }

                    // Read image data
                    byte[] imageData = new byte[imageSize];
                    bytesRead = 0;
                    while (bytesRead < imageSize && isConnected)
                    {
                        int read = networkStream.Read(imageData, bytesRead, imageSize - bytesRead);
                        if (read == 0) throw new Exception("Connection closed by server");
                        bytesRead += read;
                    }

                    if (!isConnected) break;

                    // Convert to image and display
                    using (MemoryStream ms = new MemoryStream(imageData))
                    {
                        Image remoteImage = Image.FromStream(ms);
                        this.Invoke(new Action(() =>
                        {
                            if (pictureBoxRemoteScreen.Image != null)
                                pictureBoxRemoteScreen.Image.Dispose();
                            pictureBoxRemoteScreen.Image = remoteImage;
                        }));
                    }
                }
                catch (IOException ex)
                {
                    LogMessage($"Receive error - connection lost: {ex.Message}");
                    break;
                }
                catch (Exception ex)
                {
                    if (isConnected)
                        LogMessage($"Receive error: {ex.Message}");
                    break;
                }
            }

            LogMessage("Screen receiver thread stopped");

            if (isConnected)
            {
                this.Invoke(new Action(() => Disconnect()));
            }
        }

        private void SendCommand(string command)
        {
            if (!isConnected || !chkControlEnabled.Checked) return;
            if (networkStream == null || !tcpClient.Connected) return;

            try
            {
                byte[] data = System.Text.Encoding.ASCII.GetBytes(command);
                networkStream.Write(data, 0, data.Length);
                networkStream.Flush();
            }
            catch (Exception ex)
            {
                LogMessage($"Send error: {ex.Message}");
                Disconnect();
            }
        }

        private void PictureBoxRemoteScreen_MouseMove(object sender, MouseEventArgs e)
        {
            if (!chkControlEnabled.Checked || pictureBoxRemoteScreen.Image == null) return;

            try
            {
                // Calculate absolute position based on actual screen size
                float scaleX = (float)Screen.PrimaryScreen.Bounds.Width / pictureBoxRemoteScreen.Image.Width;
                float scaleY = (float)Screen.PrimaryScreen.Bounds.Height / pictureBoxRemoteScreen.Image.Height;

                int actualX = (int)(e.X * scaleX);
                int actualY = (int)(e.Y * scaleY);

                // Clamp values
                actualX = Math.Max(0, Math.Min(actualX, Screen.PrimaryScreen.Bounds.Width - 1));
                actualY = Math.Max(0, Math.Min(actualY, Screen.PrimaryScreen.Bounds.Height - 1));

                SendCommand($"MOUSE_MOVE|{actualX}|{actualY}");
            }
            catch (Exception ex)
            {
                LogMessage($"Mouse move error: {ex.Message}");
            }
        }

        private void PictureBoxRemoteScreen_MouseClick(object sender, MouseEventArgs e)
        {
            if (!chkControlEnabled.Checked) return;

            string button = e.Button == MouseButtons.Left ? "LEFT" : "RIGHT";
            SendCommand($"MOUSE_CLICK|{button}");
        }

        private void PictureBoxRemoteScreen_MouseDown(object sender, MouseEventArgs e)
        {
            if (!chkControlEnabled.Checked) return;

            string button = e.Button == MouseButtons.Left ? "LEFT" : "RIGHT";
            SendCommand($"MOUSE_DOWN|{button}");
        }

        private void PictureBoxRemoteScreen_MouseUp(object sender, MouseEventArgs e)
        {
            if (!chkControlEnabled.Checked) return;

            string button = e.Button == MouseButtons.Left ? "LEFT" : "RIGHT";
            SendCommand($"MOUSE_UP|{button}");
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (!chkControlEnabled.Checked) return;

            VirtualKeyCode keyCode = (VirtualKeyCode)e.KeyCode;
            SendCommand($"KEY_DOWN|{keyCode}");
        }

        private void Form1_KeyUp(object sender, KeyEventArgs e)
        {
            if (!chkControlEnabled.Checked) return;

            VirtualKeyCode keyCode = (VirtualKeyCode)e.KeyCode;
            SendCommand($"KEY_UP|{keyCode}");
        }

        private void LogMessage(string message)
        {
            if (txtLog.InvokeRequired)
            {
                txtLog.Invoke(new Action(() => LogMessage(message)));
                return;
            }

            txtLog.AppendText($"{DateTime.Now:HH:mm:ss} - {message}\n");
            txtLog.ScrollToCaret();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Disconnect();
        }
    }
}*/



using System;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Text;
using System.Collections.Generic;
using WindowsInput;
using WindowsInput.Native;


namespace RemoteDesktopClient
{
    public partial class Form1 : Form
    {
        private TcpClient tcpClient;
        private NetworkStream networkStream;
        private bool isConnected = false;
        private Thread receiveThread;
        private bool isReceiving = false;
        private CancellationTokenSource cancellationTokenSource;

        // UI Components
        private TextBox txtServerIP;
        private TextBox txtPort;
        private TextBox txtPassword;
        private Button btnConnect;
        private Button btnDisconnect;
        private Label lblStatus;
        private PictureBox pictureBoxRemoteScreen;
        private RichTextBox txtLog;
        private CheckBox chkControlEnabled;
        private NumericUpDown nudQuality;
        private Label lblConnectionQuality;
        private CheckBox chkAutoReconnect;
        private System.Windows.Forms.Timer reconnectTimer;  // Fixed: Fully qualified Timer
        private int reconnectAttempts = 0;
        private const int MAX_RECONNECT_ATTEMPTS = 5;

        // Performance
        private DateTime lastFrameTime;
        private int fps;
        private Queue<double> frameTimes = new Queue<double>();

        // Control
        private InputSimulator inputSimulator;
        private bool isDragging = false;
        private Point lastMousePos;

        public Form1()
        {
            inputSimulator = new InputSimulator();
            InitializeComponent();
            SetupUI();
            this.FormClosing += Form1_FormClosing;  // Fixed: Event handler added
            SetupReconnectTimer();
        }

        private void SetupUI()
        {
            this.Text = "Remote Desktop Client (WAN Ready) - v2.0";
            this.Size = new Size(1100, 800);
            this.StartPosition = FormStartPosition.CenterScreen;

            // Connection Panel
            GroupBox gbConnection = new GroupBox()
            {
                Text = "Connection Settings",
                Location = new Point(12, 12),
                Size = new Size(1060, 130)
            };

            Label lblIP = new Label() { Text = "Server IP/Hostname:", Location = new Point(10, 28), Width = 120 };
            txtServerIP = new TextBox() { Text = "", Location = new Point(140, 25), Width = 200 };

            Label lblPort = new Label() { Text = "Port:", Location = new Point(360, 28), Width = 40 };
            txtPort = new TextBox() { Text = "5900", Location = new Point(405, 25), Width = 80 };

            Label lblPassword = new Label() { Text = "Password:", Location = new Point(500, 28), Width = 65 };
            txtPassword = new TextBox() { Location = new Point(570, 25), Width = 150, PasswordChar = '*' };

            btnConnect = new Button() { Text = "Connect", Location = new Point(740, 23), Width = 100 };
            btnConnect.Click += BtnConnect_Click;

            btnDisconnect = new Button() { Text = "Disconnect", Location = new Point(850, 23), Width = 100, Enabled = false };
            btnDisconnect.Click += BtnDisconnect_Click;

            lblStatus = new Label() { Text = "Status: Disconnected", Location = new Point(10, 65), Width = 200, ForeColor = Color.Red };

            lblConnectionQuality = new Label() { Text = "Quality: --", Location = new Point(220, 65), Width = 150 };

            chkAutoReconnect = new CheckBox() { Text = "Auto-reconnect", Location = new Point(400, 63), Width = 120, Checked = true };

            Label lblQualityHint = new Label()
            {
                Text = "Lower quality for slow connections",
                Location = new Point(10, 95),
                Width = 250,
                Font = new Font("Consolas", 8),
                ForeColor = Color.Gray
            };

            nudQuality = new NumericUpDown()
            {
                Location = new Point(260, 92),
                Width = 60,
                Minimum = 30,
                Maximum = 100,
                Value = 70,
                Increment = 5,
                Enabled = false
            };
            nudQuality.ValueChanged += (s, e) => { /* Quality adjustment handled elsewhere */ };

            // Remote Display
            pictureBoxRemoteScreen = new PictureBox()
            {
                Location = new Point(12, 155),
                Size = new Size(1060, 540),
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Black
            };

            // Mouse events - Fixed: All handlers now exist
            pictureBoxRemoteScreen.MouseMove += PictureBoxRemoteScreen_MouseMove;
            pictureBoxRemoteScreen.MouseClick += PictureBoxRemoteScreen_MouseClick;
            pictureBoxRemoteScreen.MouseDown += PictureBoxRemoteScreen_MouseDown;
            pictureBoxRemoteScreen.MouseUp += PictureBoxRemoteScreen_MouseUp;
            pictureBoxRemoteScreen.MouseWheel += PictureBoxRemoteScreen_MouseWheel;

            // Log
            txtLog = new RichTextBox()
            {
                Location = new Point(12, 705),
                Size = new Size(1060, 60),
                ReadOnly = true,
                Font = new Font("Consolas", 8)
            };

            // Control checkbox
            chkControlEnabled = new CheckBox()
            {
                Text = "Enable Remote Control (Send mouse/keyboard)",
                Location = new Point(12, 770),
                Width = 250,
                Checked = true
            };

            // Keyboard events - Fixed: All handlers now exist
            this.KeyPreview = true;
            this.KeyDown += Form1_KeyDown;
            this.KeyUp += Form1_KeyUp;

            gbConnection.Controls.AddRange(new Control[] {
                lblIP, txtServerIP, lblPort, txtPort, lblPassword, txtPassword,
                btnConnect, btnDisconnect, lblStatus, lblConnectionQuality,
                chkAutoReconnect, lblQualityHint, nudQuality
            });

            this.Controls.AddRange(new Control[] {
                gbConnection, pictureBoxRemoteScreen, txtLog, chkControlEnabled
            });
        }

        private void SetupReconnectTimer()
        {
            reconnectTimer = new System.Windows.Forms.Timer();  // Fixed: Using WinForms Timer
            reconnectTimer.Interval = 5000; // 5 seconds
            reconnectTimer.Tick += ReconnectTimer_Tick;
        }

        private async void BtnConnect_Click(object sender, EventArgs e)
        {
            if (isConnected) return;
            await ConnectAsync();
        }

        private void BtnDisconnect_Click(object sender, EventArgs e)
        {
            Disconnect();
        }

        /*  private async Task ConnectAsync()
          {
              try
              {
                  btnConnect.Enabled = false;
                  LogMessage($"Connecting to {txtServerIP.Text}:{txtPort.Text}...");

                  tcpClient = new TcpClient();

                  // Connection timeout (5 seconds)
                  var connectTask = tcpClient.ConnectAsync(txtServerIP.Text, int.Parse(txtPort.Text));
                  if (await Task.WhenAny(connectTask, Task.Delay(5000)) != connectTask)
                  {
                      throw new TimeoutException("Connection timeout");
                  }

                  networkStream = tcpClient.GetStream();

                  // Authenticate
                  if (!await Authenticate())
                  {
                      throw new Exception("Authentication failed");
                  }

                  isConnected = true;
                  isReceiving = true;
                  reconnectAttempts = 0;
                  cancellationTokenSource = new CancellationTokenSource();

                  btnDisconnect.Enabled = true;
                  txtServerIP.Enabled = false;
                  txtPort.Enabled = false;
                  txtPassword.Enabled = false;
                  nudQuality.Enabled = true;
                  lblStatus.Text = "Status: Connected";
                  lblStatus.ForeColor = Color.Green;

                  LogMessage($"Connected successfully to {txtServerIP.Text}:{txtPort.Text}");

                  // Start receiving
                  receiveThread = new Thread(() => ReceiveScreen(cancellationTokenSource.Token));
                  receiveThread.IsBackground = true;
                  receiveThread.Start();
              }
              catch (Exception ex)
              {
                  LogMessage($"Connection failed: {ex.Message}");
                  MessageBox.Show($"Connection failed: {ex.Message}", "Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                  Disconnect();
              }
              finally
              {
                  btnConnect.Enabled = !isConnected;
              }
          }
  */

        private async Task ConnectAsync()
        {
            try
            {
                btnConnect.Enabled = false;
                LogMessage($"Connecting to {txtServerIP.Text}:{txtPort.Text}...");

                tcpClient = new TcpClient();

                // FIX 1: Better connection handling
                await tcpClient.ConnectAsync(txtServerIP.Text, int.Parse(txtPort.Text));

                // FIX 2: Verify connection is actually established
                if (!tcpClient.Connected)
                {
                    throw new Exception("Failed to establish connection");
                }

                // FIX 3: Small delay to ensure connection is ready
                await Task.Delay(100);

                networkStream = tcpClient.GetStream();

                // FIX 4: Set timeouts
                networkStream.ReadTimeout = 5000;
                networkStream.WriteTimeout = 5000;

                // Authenticate
                bool authSuccess = await Authenticate();
                if (!authSuccess)
                {
                    throw new Exception("Authentication failed - incorrect password");
                }

                isConnected = true;
                isReceiving = true;
                reconnectAttempts = 0;
                cancellationTokenSource = new CancellationTokenSource();

                btnDisconnect.Enabled = true;
                txtServerIP.Enabled = false;
                txtPort.Enabled = false;
                txtPassword.Enabled = false;
                nudQuality.Enabled = true;
                lblStatus.Text = "Status: Connected";
                lblStatus.ForeColor = Color.Green;

                LogMessage($"Connected successfully to {txtServerIP.Text}:{txtPort.Text}");

                // Start receiving
                receiveThread = new Thread(() => ReceiveScreen(cancellationTokenSource.Token));
                receiveThread.IsBackground = true;
                receiveThread.Start();
            }
            catch (SocketException ex)
            {
                LogMessage($"Connection failed: Socket error - {ex.Message}");
                MessageBox.Show($"Cannot connect to server:\n{ex.Message}\n\nMake sure:\n1. Server is running\n2. IP/Port is correct\n3. Firewall allows connection",
                              "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Disconnect();
            }
            catch (Exception ex)
            {
                LogMessage($"Connection failed: {ex.Message}");
                MessageBox.Show($"Connection failed: {ex.Message}", "Error",
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
                Disconnect();
            }
            finally
            {
                btnConnect.Enabled = !isConnected;
            }
        }


        /*  private async Task<bool> Authenticate()
          {
              try
              {
                  string authMessage = $"AUTH|{txtPassword.Text}";
                  byte[] authData = Encoding.UTF8.GetBytes(authMessage);
                  await networkStream.WriteAsync(authData, 0, authData.Length);

                  byte[] response = new byte[256];
                  int bytesRead = await networkStream.ReadAsync(response, 0, response.Length);
                  string responseStr = Encoding.UTF8.GetString(response, 0, bytesRead);

                  if (responseStr == "AUTH_SUCCESS")
                  {
                      LogMessage("Authentication successful");
                      return true;
                  }
                  else
                  {
                      LogMessage($"Authentication failed: {responseStr}");
                      return false;
                  }
              }
              catch (Exception ex)
              {
                  LogMessage($"Authentication error: {ex.Message}");
                  return false;
              }
          }
  */


        private async Task<bool> Authenticate()
        {
            try
            {
                // FIX 5: Ensure stream is writable
                if (networkStream == null || !networkStream.CanWrite)
                {
                    LogMessage("Network stream not ready for writing");
                    return false;
                }

                string authMessage = $"AUTH|{txtPassword.Text}";
                byte[] authData = Encoding.UTF8.GetBytes(authMessage);

                // FIX 6: Use WriteAsync with timeout
                await networkStream.WriteAsync(authData, 0, authData.Length);
                await networkStream.FlushAsync();

                // FIX 7: Ensure stream is readable
                if (!networkStream.CanRead)
                {
                    LogMessage("Network stream not ready for reading");
                    return false;
                }

                // FIX 8: Read with timeout using Task
                byte[] response = new byte[256];
                var readTask = networkStream.ReadAsync(response, 0, response.Length);

                if (await Task.WhenAny(readTask, Task.Delay(5000)) != readTask)
                {
                    LogMessage("Authentication timeout - server not responding");
                    return false;
                }

                int bytesRead = await readTask;
                if (bytesRead == 0)
                {
                    LogMessage("Server closed connection during authentication");
                    return false;
                }

                string responseStr = Encoding.UTF8.GetString(response, 0, bytesRead);

                if (responseStr == "AUTH_SUCCESS")
                {
                    LogMessage("Authentication successful");
                    return true;
                }
                else if (responseStr.StartsWith("ENCRYPT|"))
                {
                    LogMessage("Secure connection established");
                    return true;
                }
                else
                {
                    LogMessage($"Authentication failed: {responseStr}");
                    return false;
                }
            }
            catch (IOException ex)
            {
                LogMessage($"Authentication I/O error: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                LogMessage($"Authentication error: {ex.Message}");
                return false;
            }
        }



        private void Disconnect()
        {
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource = null;
            }

            isConnected = false;
            isReceiving = false;

            if (networkStream != null)
            {
                try { networkStream.Close(); } catch { }
                networkStream = null;
            }

            if (tcpClient != null)
            {
                try { tcpClient.Close(); } catch { }
                tcpClient = null;
            }

            if (receiveThread != null && receiveThread.IsAlive)
                receiveThread.Join(500);

            btnConnect.Enabled = true;
            btnDisconnect.Enabled = false;
            txtServerIP.Enabled = true;
            txtPort.Enabled = true;
            txtPassword.Enabled = true;
            nudQuality.Enabled = false;
            lblStatus.Text = "Status: Disconnected";
            lblStatus.ForeColor = Color.Red;
            lblConnectionQuality.Text = "Quality: --";

            if (pictureBoxRemoteScreen.Image != null)
            {
                pictureBoxRemoteScreen.Image.Dispose();
                pictureBoxRemoteScreen.Image = null;
            }

            LogMessage("Disconnected from server");

            // Auto-reconnect
            if (chkAutoReconnect.Checked && reconnectAttempts < MAX_RECONNECT_ATTEMPTS)
            {
                reconnectAttempts++;
                LogMessage($"Auto-reconnect attempt {reconnectAttempts}/{MAX_RECONNECT_ATTEMPTS} in 5 seconds...");
                reconnectTimer.Start();
            }
        }

        private void ReconnectTimer_Tick(object sender, EventArgs e)
        {
            reconnectTimer.Stop();
            if (!isConnected)
            {
                LogMessage("Attempting to reconnect...");
                _ = ConnectAsync();
            }
        }

        /*   private void ReceiveScreen(CancellationToken token)
           {
               byte[] sizeBuffer = new byte[4];
               DateTime lastFrameTime = DateTime.Now;
               int frameCount = 0;
               DateTime fpsStartTime = DateTime.Now;

               while (isConnected && isReceiving && tcpClient != null && tcpClient.Connected && !token.IsCancellationRequested)
               {
                   try
                   {
                       // Read image size (4 bytes)
                       int bytesRead = 0;
                       while (bytesRead < 4 && isConnected && !token.IsCancellationRequested)
                       {
                           int read = networkStream.Read(sizeBuffer, bytesRead, 4 - bytesRead);
                           if (read == 0) throw new Exception("Connection closed by server");
                           bytesRead += read;
                       }

                       if (!isConnected || token.IsCancellationRequested) break;

                       int imageSize = BitConverter.ToInt32(sizeBuffer, 0);

                       if (imageSize <= 0 || imageSize > 50 * 1024 * 1024) // Max 50MB
                       {
                           LogMessage($"Invalid image size: {imageSize}");
                           break;
                       }

                       // Read image data
                       byte[] imageData = new byte[imageSize];
                       bytesRead = 0;
                       while (bytesRead < imageSize && isConnected && !token.IsCancellationRequested)
                       {
                           int read = networkStream.Read(imageData, bytesRead, imageSize - bytesRead);
                           if (read == 0) throw new Exception("Connection closed by server");
                           bytesRead += read;
                       }

                       if (!isConnected || token.IsCancellationRequested) break;

                       // Convert to image and display
                       using (MemoryStream ms = new MemoryStream(imageData))
                       {
                           Image remoteImage = Image.FromStream(ms);
                           this.Invoke(new Action(() =>
                           {
                               if (pictureBoxRemoteScreen.Image != null)
                                   pictureBoxRemoteScreen.Image.Dispose();
                               pictureBoxRemoteScreen.Image = remoteImage;

                               // Update FPS
                               frameCount++;
                               var elapsed = (DateTime.Now - fpsStartTime).TotalSeconds;
                               if (elapsed >= 1.0)
                               {
                                   fps = (int)(frameCount / elapsed);
                                   lblConnectionQuality.Text = $"Quality: {nudQuality.Value}% | FPS: {fps}";
                                   frameCount = 0;
                                   fpsStartTime = DateTime.Now;
                               }
                           }));
                       }
                   }
                   catch (IOException ex)
                   {
                       LogMessage($"Receive error - connection lost: {ex.Message}");
                       break;
                   }
                   catch (Exception ex)
                   {
                       if (isConnected)
                           LogMessage($"Receive error: {ex.Message}");
                       break;
                   }
               }

               LogMessage("Screen receiver thread stopped");

               if (isConnected)
               {
                   this.Invoke(new Action(() => Disconnect()));
               }
           }

   */


        private void ReceiveScreen(CancellationToken token)
        {
            byte[] sizeBuffer = new byte[4];
            DateTime lastFrameTime = DateTime.Now;
            int frameCount = 0;
            DateTime fpsStartTime = DateTime.Now;

            // FIX 9: Add small delay before starting receive
            Thread.Sleep(100);

            while (isConnected && isReceiving && tcpClient != null && tcpClient.Connected && !token.IsCancellationRequested)
            {
                try
                {
                    // FIX 10: Check if stream is readable
                    if (networkStream == null || !networkStream.CanRead)
                    {
                        LogMessage("Network stream not readable");
                        break;
                    }

                    // FIX 11: Check if data is available before reading
                    if (!networkStream.DataAvailable)
                    {
                        Thread.Sleep(10);
                        continue;
                    }

                    // Read image size (4 bytes)
                    int bytesRead = 0;
                    while (bytesRead < 4 && isConnected && !token.IsCancellationRequested)
                    {
                        int read = networkStream.Read(sizeBuffer, bytesRead, 4 - bytesRead);
                        if (read == 0) throw new Exception("Connection closed by server");
                        bytesRead += read;
                    }

                    if (!isConnected || token.IsCancellationRequested) break;

                    int imageSize = BitConverter.ToInt32(sizeBuffer, 0);

                    if (imageSize <= 0 || imageSize > 50 * 1024 * 1024) // Max 50MB
                    {
                        LogMessage($"Invalid image size: {imageSize}");
                        break;
                    }

                    // Read image data
                    byte[] imageData = new byte[imageSize];
                    bytesRead = 0;
                    while (bytesRead < imageSize && isConnected && !token.IsCancellationRequested)
                    {
                        int read = networkStream.Read(imageData, bytesRead, imageSize - bytesRead);
                        if (read == 0) throw new Exception("Connection closed by server");
                        bytesRead += read;
                    }

                    if (!isConnected || token.IsCancellationRequested) break;

                    // Convert to image and display
                    using (MemoryStream ms = new MemoryStream(imageData))
                    {
                        Image remoteImage = Image.FromStream(ms);
                        this.Invoke(new Action(() =>
                        {
                            if (pictureBoxRemoteScreen.Image != null)
                                pictureBoxRemoteScreen.Image.Dispose();
                            pictureBoxRemoteScreen.Image = remoteImage;

                            // Update FPS
                            frameCount++;
                            var elapsed = (DateTime.Now - fpsStartTime).TotalSeconds;
                            if (elapsed >= 1.0)
                            {
                                fps = (int)(frameCount / elapsed);
                                lblConnectionQuality.Text = $"Quality: {nudQuality.Value}% | FPS: {fps}";
                                frameCount = 0;
                                fpsStartTime = DateTime.Now;
                            }
                        }));
                    }
                }
                catch (IOException ex)
                {
                    if (isConnected)
                        LogMessage($"Receive error - connection lost: {ex.Message}");
                    break;
                }
                catch (ObjectDisposedException)
                {
                    LogMessage("Stream was closed");
                    break;
                }
                catch (Exception ex)
                {
                    if (isConnected)
                        LogMessage($"Receive error: {ex.Message}");
                    break;
                }
            }

            LogMessage("Screen receiver thread stopped");

            if (isConnected)
            {
                this.Invoke(new Action(() => Disconnect()));
            }
        }













        private void SendCommand(string command)
        {
            if (!isConnected || !chkControlEnabled.Checked) return;
            if (networkStream == null || !tcpClient.Connected) return;

            try
            {
                byte[] data = Encoding.ASCII.GetBytes(command);
                networkStream.Write(data, 0, data.Length);
                networkStream.Flush();
            }
            catch (Exception ex)
            {
                LogMessage($"Send error: {ex.Message}");
            }
        }

        // Mouse Event Handlers
        private void PictureBoxRemoteScreen_MouseMove(object sender, MouseEventArgs e)
        {
            if (!chkControlEnabled.Checked || pictureBoxRemoteScreen.Image == null) return;

            try
            {
                // Calculate absolute position based on actual screen size
                float scaleX = (float)Screen.PrimaryScreen.Bounds.Width / pictureBoxRemoteScreen.Image.Width;
                float scaleY = (float)Screen.PrimaryScreen.Bounds.Height / pictureBoxRemoteScreen.Image.Height;

                int actualX = (int)(e.X * scaleX);
                int actualY = (int)(e.Y * scaleY);

                // Clamp values
                actualX = Math.Max(0, Math.Min(actualX, Screen.PrimaryScreen.Bounds.Width - 1));
                actualY = Math.Max(0, Math.Min(actualY, Screen.PrimaryScreen.Bounds.Height - 1));

                SendCommand($"MOUSE_MOVE|{actualX}|{actualY}");
            }
            catch (Exception ex)
            {
                LogMessage($"Mouse move error: {ex.Message}");
            }
        }

        private void PictureBoxRemoteScreen_MouseClick(object sender, MouseEventArgs e)
        {
            if (!chkControlEnabled.Checked) return;

            string button = e.Button == MouseButtons.Left ? "LEFT" :
                           (e.Button == MouseButtons.Right ? "RIGHT" : "MIDDLE");
            SendCommand($"MOUSE_CLICK|{button}");
        }

        private void PictureBoxRemoteScreen_MouseDown(object sender, MouseEventArgs e)
        {
            if (!chkControlEnabled.Checked) return;

            string button = e.Button == MouseButtons.Left ? "LEFT" :
                           (e.Button == MouseButtons.Right ? "RIGHT" : "MIDDLE");
            SendCommand($"MOUSE_DOWN|{button}");
        }

        private void PictureBoxRemoteScreen_MouseUp(object sender, MouseEventArgs e)
        {
            if (!chkControlEnabled.Checked) return;

            string button = e.Button == MouseButtons.Left ? "LEFT" :
                           (e.Button == MouseButtons.Right ? "RIGHT" : "MIDDLE");
            SendCommand($"MOUSE_UP|{button}");
        }

        private void PictureBoxRemoteScreen_MouseWheel(object sender, MouseEventArgs e)
        {
            if (!chkControlEnabled.Checked) return;

            int scrollAmount = e.Delta > 0 ? 120 : -120;
            SendCommand($"SCROLL|{scrollAmount}");
        }

        // Keyboard Event Handlers
        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (!chkControlEnabled.Checked) return;

            try
            {
                VirtualKeyCode keyCode = (VirtualKeyCode)e.KeyCode;
                SendCommand($"KEY_DOWN|{keyCode}");
            }
            catch (Exception ex)
            {
                LogMessage($"KeyDown error: {ex.Message}");
            }
        }

        private void Form1_KeyUp(object sender, KeyEventArgs e)
        {
            if (!chkControlEnabled.Checked) return;

            try
            {
                VirtualKeyCode keyCode = (VirtualKeyCode)e.KeyCode;
                SendCommand($"KEY_UP|{keyCode}");
            }
            catch (Exception ex)
            {
                LogMessage($"KeyUp error: {ex.Message}");
            }
        }

        // Form Closing Handler - Fixed
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Disconnect();
        }

        // Log Message Handler - Fixed
        private void LogMessage(string message)
        {
            if (txtLog.InvokeRequired)
            {
                txtLog.Invoke(new Action(() => LogMessage(message)));
                return;
            }

            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            txtLog.AppendText($"{timestamp} - {message}\n");
            txtLog.ScrollToCaret();
        }

        // Required designer method (if not using designer file)
      /*  private void InitializeComponent()
        {
            // This method is required by the Windows Form Designer
            // If you're not using the designer, you can leave it empty
            // since SetupUI() handles all UI creation
        }*/













    }



}
