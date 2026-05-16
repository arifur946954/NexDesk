/*

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;
using WindowsInput;
using WindowsInput.Native;

namespace RemoteDesktopServer
{
    public partial class Form1 : Form
    {
        private TcpListener tcpListener;
        private Thread listenerThread;
        private bool isRunning = false;
        private NetworkStream networkStream;
        private TcpClient currentClient;
        private InputSimulator inputSimulator;
        private Thread sendThread;
        private Thread receiveThread;
        private bool isClientConnected = false;

        // UI Components
        private Button btnStartStop;
        private Label lblStatus;
        private TextBox txtPort;
        private PictureBox pictureBoxScreen;
        private Label lblConnectionStatus;
        private CheckBox chkControlEnabled;
        private RichTextBox txtLog;
        private Button btnFindAvailablePort;

        public Form1()
        {
            inputSimulator = new InputSimulator();
            InitializeComponent();
            SetupUI();
            this.FormClosing += Form1_FormClosing;
        }

        private void SetupUI()
        {
            this.Text = "Remote Desktop Server (Host)";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;

            var lblPort = new Label() { Text = "Port:", Location = new Point(12, 15), Width = 40 };
            txtPort = new TextBox() { Text = "5900", Location = new Point(50, 12), Width = 60 };

            // Add button to find available port
            btnFindAvailablePort = new Button() { Text = "Find Free Port", Location = new Point(120, 10), Width = 100 };
            btnFindAvailablePort.Click += BtnFindAvailablePort_Click;

            btnStartStop = new Button() { Text = "Start Server", Location = new Point(230, 10), Width = 100 };
            btnStartStop.Click += BtnStartStop_Click;

            lblStatus = new Label() { Text = "Status: Stopped", Location = new Point(340, 15), Width = 100 };
            lblConnectionStatus = new Label() { Text = "No client connected", Location = new Point(450, 15), Width = 150, ForeColor = Color.Red };

            chkControlEnabled = new CheckBox() { Text = "Allow Remote Control", Location = new Point(610, 12), Width = 150, Checked = true };

            pictureBoxScreen = new PictureBox()
            {
                Location = new Point(12, 45),
                Size = new Size(760, 460),
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Black
            };

            txtLog = new RichTextBox()
            {
                Location = new Point(12, 515),
                Size = new Size(760, 50),
                ReadOnly = true,
                Font = new Font("Consolas", 9)
            };

            this.Controls.AddRange(new Control[] { lblPort, txtPort, btnFindAvailablePort, btnStartStop,
                                                    lblStatus, lblConnectionStatus, chkControlEnabled,
                                                    pictureBoxScreen, txtLog });
        }

        private void BtnFindAvailablePort_Click(object sender, EventArgs e)
        {
            int freePort = FindFreePort();
            if (freePort > 0)
            {
                txtPort.Text = freePort.ToString();
                LogMessage($"Found free port: {freePort}");
            }
            else
            {
                LogMessage("No free ports available in range 5000-65000");
            }
        }

        private int FindFreePort()
        {
            for (int port = 5000; port < 65000; port++)
            {
                if (IsPortAvailable(port))
                    return port;
            }
            return -1;
        }

        private bool IsPortAvailable(int port)
        {
            try
            {
                TcpListener listener = new TcpListener(IPAddress.Any, port);
                listener.Start();
                listener.Stop();
                return true;
            }
            catch (SocketException)
            {
                return false;
            }
        }

        private void BtnStartStop_Click(object sender, EventArgs e)
        {
            if (!isRunning)
            {
                StartServer();
            }
            else
            {
                StopServer();
            }
        }

        private void StartServer()
        {
            try
            {
                int port = int.Parse(txtPort.Text);

                // Check if port is available before starting
                if (!IsPortAvailable(port))
                {
                    MessageBox.Show($"Port {port} is already in use. Please use a different port or click 'Find Free Port'.",
                                  "Port Conflict", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    LogMessage($"ERROR: Port {port} is already in use");
                    return;
                }

                tcpListener = new TcpListener(IPAddress.Any, port);
                listenerThread = new Thread(ListenForClients);
                listenerThread.IsBackground = true;
                listenerThread.Start();

                isRunning = true;
                btnStartStop.Text = "Stop Server";
                lblStatus.Text = "Status: Running";
                txtPort.Enabled = false;
                btnFindAvailablePort.Enabled = false;
                LogMessage($"Server started successfully on port {port}");
            }
            catch (FormatException)
            {
                MessageBox.Show("Please enter a valid port number", "Invalid Port",
                              MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (SocketException ex)
            {
                if (ex.ErrorCode == 10048) // Address already in use
                {
                    MessageBox.Show($"Port {txtPort.Text} is already in use. Please use a different port.",
                                  "Port Conflict", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    LogMessage($"ERROR: Port {txtPort.Text} is already in use");
                }
                else
                {
                    MessageBox.Show($"Socket error: {ex.Message}", "Server Error",
                                  MessageBoxButtons.OK, MessageBoxIcon.Error);
                    LogMessage($"ERROR: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting server: {ex.Message}", "Server Error",
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
                LogMessage($"ERROR: {ex.Message}");
            }
        }

        private void StopServer()
        {
            LogMessage("Stopping server...");
            isRunning = false;
            isClientConnected = false;

            // Close client connection properly
            if (networkStream != null)
            {
                try { networkStream.Close(); } catch { }
                networkStream = null;
            }

            if (currentClient != null)
            {
                try { currentClient.Close(); } catch { }
                currentClient = null;
            }

            // Stop listener
            if (tcpListener != null)
            {
                try { tcpListener.Stop(); } catch { }
                tcpListener = null;
            }

            // Wait for threads to finish
            if (listenerThread != null && listenerThread.IsAlive)
                listenerThread.Join(1000);

            if (sendThread != null && sendThread.IsAlive)
                sendThread.Join(500);

            if (receiveThread != null && receiveThread.IsAlive)
                receiveThread.Join(500);

            btnStartStop.Text = "Start Server";
            lblStatus.Text = "Status: Stopped";
            txtPort.Enabled = true;
            btnFindAvailablePort.Enabled = true;
            lblConnectionStatus.Text = "No client connected";
            lblConnectionStatus.ForeColor = Color.Red;

            if (pictureBoxScreen.Image != null)
            {
                pictureBoxScreen.Image.Dispose();
                pictureBoxScreen.Image = null;
            }

            LogMessage("Server stopped");
        }

        private void ListenForClients()
        {
            try
            {
                tcpListener.Start();
                LogMessage("Listening for client connections...");

                while (isRunning)
                {
                    try
                    {
                        if (tcpListener.Pending())
                        {
                            // Accept new client
                            currentClient = tcpListener.AcceptTcpClient();
                            networkStream = currentClient.GetStream();
                            isClientConnected = true;

                            this.Invoke(new Action(() =>
                            {
                                lblConnectionStatus.Text = "Client connected!";
                                lblConnectionStatus.ForeColor = Color.Green;
                                LogMessage("Client connected from " + currentClient.Client.RemoteEndPoint.ToString());
                            }));

                            // Start sending screen captures
                            sendThread = new Thread(SendScreen);
                            sendThread.IsBackground = true;
                            sendThread.Start();

                            // Start receiving commands
                            receiveThread = new Thread(ReceiveCommands);
                            receiveThread.IsBackground = true;
                            receiveThread.Start();
                        }
                        Thread.Sleep(100);
                    }
                    catch (Exception ex)
                    {
                        if (isRunning)
                        {
                            LogMessage($"Listener error: {ex.Message}");
                            this.Invoke(new Action(() =>
                            {
                                lblConnectionStatus.Text = "Connection error";
                                lblConnectionStatus.ForeColor = Color.Red;
                            }));
                            isClientConnected = false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Listener thread error: {ex.Message}");
            }
        }

        private void SendScreen()
        {
            LogMessage("Screen sender thread started");

            while (isRunning && isClientConnected && currentClient != null && currentClient.Connected)
            {
                try
                {
                    Rectangle bounds = Screen.PrimaryScreen.Bounds;
                    using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height))
                    {
                        using (Graphics g = Graphics.FromImage(bitmap))
                        {
                            g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
                        }

                        using (MemoryStream ms = new MemoryStream())
                        {
                            // Compress image
                            bitmap.Save(ms, ImageFormat.Jpeg);
                            byte[] imageData = ms.ToArray();

                            // Send image size
                            byte[] sizeBytes = BitConverter.GetBytes(imageData.Length);
                            networkStream.Write(sizeBytes, 0, 4);
                            networkStream.Flush();

                            // Send image data
                            networkStream.Write(imageData, 0, imageData.Length);
                            networkStream.Flush();

                            // Update preview
                            this.Invoke(new Action(() =>
                            {
                                if (pictureBoxScreen.Image != null)
                                    pictureBoxScreen.Image.Dispose();
                                pictureBoxScreen.Image = new Bitmap(bitmap);
                            }));
                        }
                    }

                    Thread.Sleep(100); // 10 FPS
                }
                catch (IOException ex)
                {
                    LogMessage($"Screen send error - client disconnected: {ex.Message}");
                    break;
                }
                catch (Exception ex)
                {
                    LogMessage($"Screen send error: {ex.Message}");
                    break;
                }
            }

            LogMessage("Screen sender thread stopped");

            // Clean up on thread exit
            this.Invoke(new Action(() =>
            {
                if (isClientConnected)
                {
                    isClientConnected = false;
                    lblConnectionStatus.Text = "Client disconnected";
                    lblConnectionStatus.ForeColor = Color.Red;
                }
            }));
        }

        private void ReceiveCommands()
        {
            LogMessage("Command receiver thread started");
            byte[] buffer = new byte[4096];

            while (isRunning && isClientConnected && currentClient != null && currentClient.Connected)
            {
                try
                {
                    if (networkStream.DataAvailable)
                    {
                        int bytesRead = networkStream.Read(buffer, 0, buffer.Length);
                        if (bytesRead > 0)
                        {
                            string command = System.Text.Encoding.ASCII.GetString(buffer, 0, bytesRead);
                            ProcessCommand(command);
                        }
                    }
                    Thread.Sleep(10);
                }
                catch (IOException ex)
                {
                    LogMessage($"Command receive error - client disconnected: {ex.Message}");
                    break;
                }
                catch (Exception ex)
                {
                    LogMessage($"Command receive error: {ex.Message}");
                    break;
                }
            }

            LogMessage("Command receiver thread stopped");

            // Clean up on thread exit
            this.Invoke(new Action(() =>
            {
                if (isClientConnected)
                {
                    isClientConnected = false;
                    lblConnectionStatus.Text = "Client disconnected";
                    lblConnectionStatus.ForeColor = Color.Red;
                }
            }));
        }

        private void ProcessCommand(string command)
        {
            if (!chkControlEnabled.Checked) return;

            try
            {
                string[] parts = command.Split('|');
                if (parts.Length < 2) return;

                string cmdType = parts[0];

                switch (cmdType)
                {
                    case "MOUSE_MOVE":
                        if (parts.Length == 3)
                        {
                            int x = int.Parse(parts[1]);
                            int y = int.Parse(parts[2]);
                            Cursor.Position = new Point(x, y);
                        }
                        break;

                    case "MOUSE_CLICK":
                        if (parts.Length == 2)
                        {
                            if (parts[1] == "LEFT")
                                inputSimulator.Mouse.LeftButtonClick();
                            else if (parts[1] == "RIGHT")
                                inputSimulator.Mouse.RightButtonClick();
                        }
                        break;

                    case "MOUSE_DOWN":
                        if (parts.Length == 2 && parts[1] == "LEFT")
                            inputSimulator.Mouse.LeftButtonDown();
                        break;

                    case "MOUSE_UP":
                        if (parts.Length == 2 && parts[1] == "LEFT")
                            inputSimulator.Mouse.LeftButtonUp();
                        break;

                    case "KEY_DOWN":
                        if (parts.Length == 2)
                        {
                            VirtualKeyCode key = (VirtualKeyCode)Enum.Parse(typeof(VirtualKeyCode), parts[1]);
                            inputSimulator.Keyboard.KeyDown(key);
                        }
                        break;

                    case "KEY_UP":
                        if (parts.Length == 2)
                        {
                            VirtualKeyCode key = (VirtualKeyCode)Enum.Parse(typeof(VirtualKeyCode), parts[1]);
                            inputSimulator.Keyboard.KeyUp(key);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Command error: {ex.Message}");
            }
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
            StopServer();
        }
    }
}*/



using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.Serialization.Json;
using System.Collections.Generic;
using WindowsInput;
using WindowsInput.Native;
using Open.Nat;


namespace RemoteDesktopServer
{
    public partial class Form1 : Form
    {
        // Connection management
        private TcpListener tcpListener;
        private Thread listenerThread;
        private bool isRunning = false;
        private NetworkStream networkStream;
        private TcpClient currentClient;
        private InputSimulator inputSimulator;

        // Threading
        private Thread sendThread;
        private Thread receiveThread;
        private bool isClientConnected = false;
        private CancellationTokenSource cancellationTokenSource;

        // Advanced features
        private Dictionary<string, string> connectedClients = new Dictionary<string, string>();
        private SimpleAES encryption;
        private bool useEncryption = true;
        private string serverPassword;

        // Performance optimization
        private Rectangle previousScreenBounds;
        private byte[] previousFrameHash;
        private int frameQuality = 70; // JPEG quality (1-100)
        private DateTime lastFrameTime;
        private int fpsTarget = 30;
        private int actualFPS;

        // NAT Traversal
        private UPnPManager upnpManager;
        private bool useUPnP = true;

        // UI Components
        private Button btnStartStop;
        private Label lblStatus;
        private TextBox txtPort;
        private PictureBox pictureBoxScreen;
        private Label lblConnectionStatus;
        private CheckBox chkControlEnabled;
        private RichTextBox txtLog;
        private Button btnFindAvailablePort;
        private Button btnUPnPSetup;
        private NumericUpDown nudQuality;
        private NumericUpDown nudFPS;
        private Label lblFPS;
        private CheckBox chkEncryption;
        private TextBox txtPassword;
        private Label lblPublicIP;
        private Button btnCopyPublicIP;

        public Form1()
        {
            inputSimulator = new InputSimulator();
            encryption = new SimpleAES();
            upnpManager = new UPnPManager();
            InitializeComponent();
            SetupUI();
            this.FormClosing += Form1_FormClosing;
            GetPublicIPAddress();
        }




        private void SetupUI()
        {
            this.Text = "Remote Desktop Server (WAN Ready) - v2.0";
            this.Size = new Size(900, 750);
            this.StartPosition = FormStartPosition.CenterScreen;

            // Connection Settings Group
            GroupBox gbConnection = new GroupBox()
            {
                Text = "Connection Settings",
                Location = new Point(12, 12),
                Size = new Size(860, 110)
            };

            var lblPort = new Label() { Text = "Port:", Location = new Point(10, 25), Width = 40 };
            txtPort = new TextBox() { Text = "5900", Location = new Point(55, 22), Width = 80 };

            btnFindAvailablePort = new Button()
            {
                Text = "Find Free Port",
                Location = new Point(145, 20),
                Width = 100
            };
            btnFindAvailablePort.Click += BtnFindAvailablePort_Click;

            btnStartStop = new Button()
            {
                Text = "Start Server",
                Location = new Point(260, 20),
                Width = 100
            };
            btnStartStop.Click += BtnStartStop_Click;

            lblStatus = new Label()
            {
                Text = "Status: Stopped",
                Location = new Point(375, 25),
                Width = 120
            };

            lblPublicIP = new Label()
            {
                Text = "Public IP: Detecting...",
                Location = new Point(10, 55),
                Width = 350,
                Font = new Font("Consolas", 9),
                ForeColor = Color.Blue
            };

            btnCopyPublicIP = new Button()
            {
                Text = "Copy IP",
                Location = new Point(370, 52),
                Width = 70
            };
            btnCopyPublicIP.Click += (s, e) =>
            {
                Clipboard.SetText(lblPublicIP.Text.Replace("Public IP: ", ""));
                LogMessage("Public IP copied to clipboard");
            };

            // Security Group
            GroupBox gbSecurity = new GroupBox()
            {
                Text = "Security Settings",
                Location = new Point(12, 130),
                Size = new Size(860, 70)
            };

            chkEncryption = new CheckBox()
            {
                Text = "Enable Encryption (AES-256)",
                Location = new Point(10, 20),
                Width = 180,
                Checked = true
            };
            chkEncryption.CheckedChanged += (s, e) => useEncryption = chkEncryption.Checked;

            Label lblPassword = new Label() { Text = "Password:", Location = new Point(200, 22), Width = 65 };
            txtPassword = new TextBox()
            {
                Location = new Point(270, 20),
                Width = 150,
                PasswordChar = '*'
            };

            btnUPnPSetup = new Button()
            {
                Text = "Setup UPnP Port Forwarding",
                Location = new Point(440, 18),
                Width = 180
            };
            btnUPnPSetup.Click += BtnUPnPSetup_Click;

            // Performance Group
            GroupBox gbPerformance = new GroupBox()
            {
                Text = "Performance Settings",
                Location = new Point(12, 210),
                Size = new Size(860, 70)
            };

            Label lblQuality = new Label() { Text = "Quality:", Location = new Point(10, 28), Width = 50 };
            nudQuality = new NumericUpDown()
            {
                Location = new Point(65, 25),
                Width = 60,
                Minimum = 30,
                Maximum = 100,
                Value = 70,
                Increment = 5
            };
            nudQuality.ValueChanged += (s, e) => frameQuality = (int)nudQuality.Value;

            Label lblFPSTarget = new Label() { Text = "Target FPS:", Location = new Point(140, 28), Width = 65 };
            nudFPS = new NumericUpDown()
            {
                Location = new Point(210, 25),
                Width = 60,
                Minimum = 5,
                Maximum = 60,
                Value = 30,
                Increment = 5
            };
            nudFPS.ValueChanged += (s, e) => fpsTarget = (int)nudFPS.Value;

            lblFPS = new Label()
            {
                Text = "Actual FPS: 0",
                Location = new Point(290, 28),
                Width = 120,
                ForeColor = Color.Green
            };

            // Display
            pictureBoxScreen = new PictureBox()
            {
                Location = new Point(12, 290),
                Size = new Size(860, 360),
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Black
            };

            lblConnectionStatus = new Label()
            {
                Text = "No client connected",
                Location = new Point(12, 655),
                Width = 300,
                ForeColor = Color.Red
            };

            chkControlEnabled = new CheckBox()
            {
                Text = "Allow Remote Control",
                Location = new Point(320, 655),
                Width = 150,
                Checked = true
            };

            txtLog = new RichTextBox()
            {
                Location = new Point(12, 680),
                Size = new Size(860, 40),
                ReadOnly = true,
                Font = new Font("Consolas", 8)
            };

            gbConnection.Controls.AddRange(new Control[] { lblPort, txtPort, btnFindAvailablePort,
                btnStartStop, lblStatus, lblPublicIP, btnCopyPublicIP });
            gbSecurity.Controls.AddRange(new Control[] { chkEncryption, lblPassword, txtPassword, btnUPnPSetup });
            gbPerformance.Controls.AddRange(new Control[] { lblQuality, nudQuality, lblFPSTarget, nudFPS, lblFPS });

            this.Controls.AddRange(new Control[] { gbConnection, gbSecurity, gbPerformance,
                pictureBoxScreen, lblConnectionStatus, chkControlEnabled, txtLog });
        }

        private async void GetPublicIPAddress()
        {
            try
            {
                using (var client = new WebClient())
                {
                    string publicIP = await client.DownloadStringTaskAsync("http://api.ipify.org");
                    lblPublicIP.Text = $"Public IP: {publicIP.Trim()}";
                }
            }
            catch
            {
                lblPublicIP.Text = "Public IP: Unable to detect";
            }
        }

        private void BtnUPnPSetup_Click(object sender, EventArgs e)
        {
            try
            {
                int port = int.Parse(txtPort.Text);
                if (upnpManager.ForwardPort(port, ProtocolType.Tcp, "RemoteDesktopServer"))
                {
                    LogMessage($"UPnP: Successfully forwarded port {port}");
                    MessageBox.Show($"Port {port} forwarded successfully!\n\n" +
                                  $"Clients can now connect using your public IP: {lblPublicIP.Text.Replace("Public IP: ", "")}",
                                  "UPnP Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    LogMessage("UPnP: Failed to forward port. Manual port forwarding may be required.");
                    MessageBox.Show("UPnP port forwarding failed.\n\n" +
                                  "Please manually forward port " + port + " in your router settings.",
                                  "UPnP Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"UPnP Error: {ex.Message}");
            }
        }

        private async void BtnStartStop_Click(object sender, EventArgs e)
        {
            if (!isRunning)
            {
                await StartServerAsync();
            }
            else
            {
                StopServer();
            }
        }

        private async Task StartServerAsync()
        {
            try
            {
                int port = int.Parse(txtPort.Text);
                serverPassword = txtPassword.Text;

                if (useUPnP)
                {
                    await Task.Run(() => upnpManager.ForwardPort(port, ProtocolType.Tcp, "RemoteDesktopServer"));
                }

                tcpListener = new TcpListener(IPAddress.Any, port);
                listenerThread = new Thread(ListenForClients);
                listenerThread.IsBackground = true;
                listenerThread.Start();

                isRunning = true;
                btnStartStop.Text = "Stop Server";
                lblStatus.Text = "Status: Running";
                txtPort.Enabled = false;
                btnFindAvailablePort.Enabled = false;
                btnUPnPSetup.Enabled = false;
                txtPassword.Enabled = false;

                LogMessage($"Server started on port {port}");
                LogMessage($"Public IP: {lblPublicIP.Text.Replace("Public IP: ", "")}");
                LogMessage($"Clients can connect using the public IP above");
            }
            catch (Exception ex)
            {
                LogMessage($"ERROR: {ex.Message}");
                MessageBox.Show($"Error starting server: {ex.Message}");
            }
        }

        private void StopServer()
        {
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource = null;
            }

            isRunning = false;
            isClientConnected = false;

            networkStream?.Close();
            currentClient?.Close();
            tcpListener?.Stop();

            if (useUPnP)
            {
                int port = int.Parse(txtPort.Text);
                upnpManager.RemoveForward(port, ProtocolType.Tcp);
            }

            btnStartStop.Text = "Start Server";
            lblStatus.Text = "Status: Stopped";
            txtPort.Enabled = true;
            btnFindAvailablePort.Enabled = true;
            btnUPnPSetup.Enabled = true;
            txtPassword.Enabled = true;
            lblConnectionStatus.Text = "No client connected";
            lblConnectionStatus.ForeColor = Color.Red;

            LogMessage("Server stopped");
        }

        private void ListenForClients()
        {
            try
            {
                tcpListener.Start();
                LogMessage("Listening for connections...");

                while (isRunning)
                {
                    if (tcpListener.Pending())
                    {
                        currentClient = tcpListener.AcceptTcpClient();
                        networkStream = currentClient.GetStream();

                        // Handle authentication
                        if (AuthenticateClient())
                        {
                            isClientConnected = true;
                            cancellationTokenSource = new CancellationTokenSource();

                            this.Invoke(new Action(() =>
                            {
                                lblConnectionStatus.Text = $"Client connected: {currentClient.Client.RemoteEndPoint}";
                                lblConnectionStatus.ForeColor = Color.Green;
                                LogMessage($"Client authenticated from {currentClient.Client.RemoteEndPoint}");
                            }));

                            sendThread = new Thread(() => SendScreen(cancellationTokenSource.Token));
                            sendThread.IsBackground = true;
                            sendThread.Start();

                            receiveThread = new Thread(() => ReceiveCommands(cancellationTokenSource.Token));
                            receiveThread.IsBackground = true;
                            receiveThread.Start();
                        }
                        else
                        {
                            networkStream.Close();
                            currentClient.Close();
                            LogMessage("Authentication failed - connection rejected");
                        }
                    }
                    Thread.Sleep(100);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Listener error: {ex.Message}");
            }
        }

        /*   private bool AuthenticateClient()
           {
               try
               {
                   byte[] buffer = new byte[256];
                   int bytesRead = networkStream.Read(buffer, 0, buffer.Length);
                   string authMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                   if (authMessage.StartsWith("AUTH|"))
                   {
                       string clientPassword = authMessage.Substring(5);

                       if (string.IsNullOrEmpty(serverPassword) || clientPassword == serverPassword)
                       {
                           // Send success response
                           byte[] response = Encoding.UTF8.GetBytes("AUTH_SUCCESS");
                           networkStream.Write(response, 0, response.Length);

                           // Send encryption key if enabled
                           if (useEncryption)
                           {
                               byte[] keyResponse = Encoding.UTF8.GetBytes($"ENCRYPT|{encryption.GetPublicKey()}");
                               networkStream.Write(keyResponse, 0, keyResponse.Length);
                           }

                           return true;
                       }
                   }

                   byte[] failResponse = Encoding.UTF8.GetBytes("AUTH_FAILED");
                   networkStream.Write(failResponse, 0, failResponse.Length);
                   return false;
               }
               catch
               {
                   return false;
               }
           }

   */

        private bool AuthenticateClient()
        {
            try
            {
                byte[] buffer = new byte[256];
                int bytesRead = networkStream.Read(buffer, 0, buffer.Length);

                if (bytesRead == 0) return false;

                string authMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                if (authMessage.StartsWith("AUTH|"))
                {
                    string clientPassword = authMessage.Substring(5);

                    if (string.IsNullOrEmpty(serverPassword) || clientPassword == serverPassword)
                    {
                        // FIX: Make sure to send response immediately
                        byte[] response = Encoding.UTF8.GetBytes("AUTH_SUCCESS");
                        networkStream.Write(response, 0, response.Length);
                        networkStream.Flush();
                        return true;
                    }
                }

                byte[] failResponse = Encoding.UTF8.GetBytes("AUTH_FAILED");
                networkStream.Write(failResponse, 0, failResponse.Length);
                networkStream.Flush();
                return false;
            }
            catch (Exception ex)
            {
                LogMessage($"Auth error: {ex.Message}");
                return false;
            }
        }







        private void SendScreen(CancellationToken token)
        {
            var lastSendTime = DateTime.Now;
            var frameTimes = new Queue<double>();

            // Get JPEG encoder once for performance
            ImageCodecInfo jpegCodec = GetJpegCodec();

            while (isRunning && isClientConnected && currentClient?.Connected == true && !token.IsCancellationRequested)
            {
                try
                {
                    var frameStartTime = DateTime.Now;
                    var targetFrameTime = 1000.0 / fpsTarget;

                    Rectangle bounds = Screen.PrimaryScreen.Bounds;

                    using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height))
                    {
                        using (Graphics g = Graphics.FromImage(bitmap))
                        {
                            g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
                        }

                        // Calculate frame hash for change detection
                        byte[] currentHash = ComputeImageHash(bitmap);

                        if (previousFrameHash == null || !CompareHash(currentHash, previousFrameHash))
                        {
                            using (MemoryStream ms = new MemoryStream())
                            {
                                // FIXED: Use fully qualified Encoder class
                                System.Drawing.Imaging.Encoder qualityEncoder = System.Drawing.Imaging.Encoder.Quality;

                                // FIXED: Convert int to long (EncoderParameter accepts long)
                                long qualityValue = frameQuality;

                                EncoderParameters encoderParams = new EncoderParameters(1);
                                encoderParams.Param[0] = new EncoderParameter(qualityEncoder, qualityValue);

                                bitmap.Save(ms, jpegCodec, encoderParams);
                                byte[] imageData = ms.ToArray();

                                if (useEncryption)
                                {
                                    imageData = encryption.Encrypt(imageData);
                                }

                                byte[] sizeBytes = BitConverter.GetBytes(imageData.Length);
                                lock (networkStream)
                                {
                                    networkStream.Write(sizeBytes, 0, 4);
                                    networkStream.Write(imageData, 0, imageData.Length);
                                    networkStream.Flush();
                                }

                                previousFrameHash = currentHash;
                            }

                            // Update preview
                            this.Invoke(new Action(() =>
                            {
                                if (pictureBoxScreen.Image != null)
                                    pictureBoxScreen.Image.Dispose();
                                pictureBoxScreen.Image = new Bitmap(bitmap);
                            }));
                        }
                    }

                    // FPS calculation and rate limiting
                    var frameTime = (DateTime.Now - frameStartTime).TotalMilliseconds;
                    frameTimes.Enqueue(frameTime);
                    while (frameTimes.Count > 30) frameTimes.Dequeue();

                    var avgFrameTime = frameTimes.Average();
                    actualFPS = (int)(1000.0 / avgFrameTime);

                    this.Invoke(new Action(() =>
                    {
                        lblFPS.Text = $"Actual FPS: {actualFPS}";
                        if (actualFPS >= fpsTarget - 5)
                            lblFPS.ForeColor = Color.Green;
                        else if (actualFPS >= fpsTarget / 2)
                            lblFPS.ForeColor = Color.Orange;
                        else
                            lblFPS.ForeColor = Color.Red;
                    }));

                    var elapsed = (DateTime.Now - lastSendTime).TotalMilliseconds;
                    if (elapsed < targetFrameTime)
                    {
                        Thread.Sleep((int)(targetFrameTime - elapsed));
                    }
                    lastSendTime = DateTime.Now;
                }
                catch (Exception ex)
                {
                    LogMessage($"Send error: {ex.Message}");
                    break;
                }
            }
        }

        private ImageCodecInfo GetJpegEncoder()
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.MimeType == "image/jpeg")
                    return codec;
            }
            return null;
        }
        /*  private ImageCodecInfo GetJpegCodec()
          {
              ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
              foreach (ImageCodecInfo codec in codecs)
              {
                  if (codec.MimeType == "image/jpeg")
                      return codec;
              }
              return null;
          }*/
        private void ReceiveCommands(CancellationToken token)
        {
            byte[] buffer = new byte[4096];

            while (isRunning && isClientConnected && currentClient?.Connected == true && !token.IsCancellationRequested)
            {
                try
                {
                    if (networkStream.DataAvailable)
                    {
                        int bytesRead = networkStream.Read(buffer, 0, buffer.Length);
                        if (bytesRead > 0)
                        {
                            string command = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                            if (useEncryption && command.StartsWith("ENCRYPTED|"))
                            {
                                command = encryption.DecryptString(command.Substring(10));
                            }
                            ProcessCommand(command);
                        }
                    }
                    Thread.Sleep(5);
                }
                catch (Exception ex)
                {
                    LogMessage($"Command error: {ex.Message}");
                    break;
                }
            }
        }

        private void ProcessCommand(string command)
        {
            if (!chkControlEnabled.Checked) return;

            try
            {
                string[] parts = command.Split('|');
                if (parts.Length < 2) return;

                switch (parts[0])
                {
                    case "MOUSE_MOVE":
                        if (parts.Length == 3)
                        {
                            int x = int.Parse(parts[1]);
                            int y = int.Parse(parts[2]);
                            Cursor.Position = new Point(x, y);
                        }
                        break;
                    case "MOUSE_CLICK":
                        if (parts[1] == "LEFT")
                            inputSimulator.Mouse.LeftButtonClick();
                        else if (parts[1] == "RIGHT")
                            inputSimulator.Mouse.RightButtonClick();
                        break;
                    case "MOUSE_DOWN":
                        if (parts[1] == "LEFT")
                            inputSimulator.Mouse.LeftButtonDown();
                        break;
                    case "MOUSE_UP":
                        if (parts[1] == "LEFT")
                            inputSimulator.Mouse.LeftButtonUp();
                        break;
                    case "KEY_DOWN":
                        VirtualKeyCode key = (VirtualKeyCode)Enum.Parse(typeof(VirtualKeyCode), parts[1]);
                        inputSimulator.Keyboard.KeyDown(key);
                        break;
                    case "KEY_UP":
                        key = (VirtualKeyCode)Enum.Parse(typeof(VirtualKeyCode), parts[1]);
                        inputSimulator.Keyboard.KeyUp(key);
                        break;
                    case "SCROLL":
                        int scrollAmount = int.Parse(parts[1]);
                        inputSimulator.Mouse.VerticalScroll(scrollAmount);
                        break;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Command error: {ex.Message}");
            }
        }

        private byte[] ComputeImageHash(Bitmap bitmap)
        {
            using (var ms = new MemoryStream())
            {
                bitmap.Save(ms, ImageFormat.Jpeg);
                using (var sha256 = SHA256.Create())
                {
                    return sha256.ComputeHash(ms.ToArray());
                }
            }
        }

        private bool CompareHash(byte[] hash1, byte[] hash2)
        {
            if (hash1.Length != hash2.Length) return false;
            for (int i = 0; i < hash1.Length; i++)
            {
                if (hash1[i] != hash2[i]) return false;
            }
            return true;
        }

        private ImageCodecInfo GetJpegCodec()
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.MimeType == "image/jpeg")
                    return codec;
            }
            return null;
        }

        private void BtnFindAvailablePort_Click(object sender, EventArgs e)
        {
            for (int port = 5000; port < 65000; port++)
            {
                if (IsPortAvailable(port))
                {
                    txtPort.Text = port.ToString();
                    LogMessage($"Found available port: {port}");
                    return;
                }
            }
            LogMessage("No available ports found");
        }

        private bool IsPortAvailable(int port)
        {
            try
            {
                TcpListener listener = new TcpListener(IPAddress.Any, port);
                listener.Start();
                listener.Stop();
                return true;
            }
            catch
            {
                return false;
            }
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
            StopServer();
        }
    }

    // AES Encryption Class
    public class SimpleAES
    {
        private byte[] key;
        private byte[] iv;
        private RSACryptoServiceProvider rsa;

        public SimpleAES()
        {
            rsa = new RSACryptoServiceProvider(2048);
            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.GenerateKey();
                aes.GenerateIV();
                key = aes.Key;
                iv = aes.IV;
            }
        }

        public string GetPublicKey()
        {
            return Convert.ToBase64String(rsa.ExportParameters(false).Modulus);
        }

        public byte[] Encrypt(byte[] data)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                using (var ms = new MemoryStream())
                using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    cs.Write(data, 0, data.Length);
                    cs.FlushFinalBlock();
                    return ms.ToArray();
                }
            }
        }

        public string DecryptString(string encryptedBase64)
        {
            byte[] data = Convert.FromBase64String(encryptedBase64);
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                using (var ms = new MemoryStream(data))
                using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read))
                using (var sr = new StreamReader(cs))
                {
                    return sr.ReadToEnd();
                }
            }
        }
    }

    // UPnP Port Forwarding Manager
    public class UPnPManager
    {
        private NatDevice device;

        public UPnPManager()
        {
            DiscoverDevice();
        }

        private async void DiscoverDevice()
        {
            try
            {
                var discoverer = new NatDiscoverer();
                var cancellationTokenSource = new CancellationTokenSource(10000); // 10 second timeout
                device = await discoverer.DiscoverDeviceAsync(PortMapper.Upnp, cancellationTokenSource);
            }
            catch (NatDeviceNotFoundException)
            {
                Console.WriteLine("UPnP device not found on network");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UPnP discovery error: {ex.Message}");
            }
        }

        public bool ForwardPort(int port, ProtocolType protocol, string description)
        {
            try
            {
                if (device != null)
                {
                    var mapping = new Mapping(
                        protocol == ProtocolType.Tcp ? Protocol.Tcp : Protocol.Udp,
                        port,
                        port,
                        3600, // Lease duration in seconds (1 hour)
                        description
                    );

                    device.CreatePortMapAsync(mapping).Wait();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UPnP forward error: {ex.Message}");
            }
            return false;
        }

        public bool RemoveForward(int port, ProtocolType protocol)
        {
            try
            {
                if (device != null)
                {
                    var mapping = new Mapping(
                        protocol == ProtocolType.Tcp ? Protocol.Tcp : Protocol.Udp,
                        port,
                        port,
                        0,
                        ""
                    );

                    device.DeletePortMapAsync(mapping).Wait();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UPnP remove error: {ex.Message}");
            }
            return false;
        }




    




 











    }


}




