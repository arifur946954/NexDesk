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

        // UI Components
        private Button btnStartStop;
        private Label lblStatus;
        private TextBox txtPort;
        private Label lblPort;
        private PictureBox pictureBoxScreen;
        private Label lblConnectionStatus;
        private CheckBox chkControlEnabled;
        private RichTextBox txtLog;

        public Form1()
        {
            InitializeComponent();
            inputSimulator = new InputSimulator();
            SetupUI();
        }

        private void SetupUI()
        {
            this.Text = "Remote Desktop Server (Host)";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;

            // Port input
            lblPort = new Label() { Text = "Port:", Location = new Point(12, 15), Width = 40 };
            txtPort = new TextBox() { Text = "5900", Location = new Point(50, 12), Width = 60 };

            // Start/Stop button
            btnStartStop = new Button() { Text = "Start Server", Location = new Point(120, 10), Width = 100 };
            btnStartStop.Click += BtnStartStop_Click;

            // Status labels
            lblStatus = new Label() { Text = "Status: Stopped", Location = new Point(230, 15), Width = 100 };
            lblConnectionStatus = new Label() { Text = "No client connected", Location = new Point(340, 15), Width = 150, ForeColor = Color.Red };

            // Control toggle
            chkControlEnabled = new CheckBox() { Text = "Allow Remote Control", Location = new Point(500, 12), Width = 150, Checked = true };

            // Screen preview
            pictureBoxScreen = new PictureBox()
            {
                Location = new Point(12, 45),
                Size = new Size(760, 460),
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.Zoom
            };

            // Log area
            txtLog = new RichTextBox()
            {
                Location = new Point(12, 515),
                Size = new Size(760, 50),
                ReadOnly = true,
                Font = new Font("Consolas", 9)
            };

            this.Controls.AddRange(new Control[] { lblPort, txtPort, btnStartStop, lblStatus,
                                                    lblConnectionStatus, chkControlEnabled,
                                                    pictureBoxScreen, txtLog });
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
                tcpListener = new TcpListener(IPAddress.Any, port);
                listenerThread = new Thread(ListenForClients);
                listenerThread.IsBackground = true;
                listenerThread.Start();

                isRunning = true;
                btnStartStop.Text = "Stop Server";
                lblStatus.Text = "Status: Running";
                txtPort.Enabled = false;
                LogMessage($"Server started on port {port}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting server: {ex.Message}");
            }
        }

        private void StopServer()
        {
            isRunning = false;
            tcpListener?.Stop();
            currentClient?.Close();
            networkStream?.Close();
            listenerThread?.Join(1000);

            btnStartStop.Text = "Start Server";
            lblStatus.Text = "Status: Stopped";
            txtPort.Enabled = true;
            lblConnectionStatus.Text = "No client connected";
            lblConnectionStatus.ForeColor = Color.Red;
            pictureBoxScreen.Image = null;
            LogMessage("Server stopped");
        }

        private void ListenForClients()
        {
            tcpListener.Start();

            while (isRunning)
            {
                try
                {
                    if (tcpListener.Pending())
                    {
                        currentClient = tcpListener.AcceptTcpClient();
                        networkStream = currentClient.GetStream();

                        this.Invoke(new Action(() =>
                        {
                            lblConnectionStatus.Text = "Client connected!";
                            lblConnectionStatus.ForeColor = Color.Green;
                            LogMessage("Client connected");
                        }));

                        // Start sending screen captures
                        Thread sendThread = new Thread(SendScreen);
                        sendThread.IsBackground = true;
                        sendThread.Start();

                        // Start receiving commands
                        Thread receiveThread = new Thread(ReceiveCommands);
                        receiveThread.IsBackground = true;
                        receiveThread.Start();
                    }
                    Thread.Sleep(100);
                }
                catch (Exception ex)
                {
                    if (isRunning)
                        LogMessage($"Listener error: {ex.Message}");
                }
            }
        }

        private void SendScreen()
        {
            while (isRunning && currentClient != null && currentClient.Connected)
            {
                try
                {
                    // Capture the entire desktop
                    Rectangle bounds = Screen.PrimaryScreen.Bounds;
                    using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height))
                    {
                        using (Graphics g = Graphics.FromImage(bitmap))
                        {
                            g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
                        }

                        // Convert to JPEG for smaller size
                        using (MemoryStream ms = new MemoryStream())
                        {
                            bitmap.Save(ms, ImageFormat.Jpeg);
                            byte[] imageData = ms.ToArray();

                            // Send image size first (4 bytes), then image data
                            byte[] sizeBytes = BitConverter.GetBytes(imageData.Length);
                            networkStream.Write(sizeBytes, 0, 4);
                            networkStream.Write(imageData, 0, imageData.Length);

                            // Update preview on server UI
                            this.Invoke(new Action(() =>
                            {
                                if (pictureBoxScreen.Image != null)
                                    pictureBoxScreen.Image.Dispose();
                                pictureBoxScreen.Image = new Bitmap(bitmap);
                            }));
                        }
                    }

                    // Control frame rate (about 10 FPS)
                    Thread.Sleep(100);
                }
                catch (Exception ex)
                {
                    LogMessage($"Send error: {ex.Message}");
                    break;
                }
            }
        }

        private void ReceiveCommands()
        {
            byte[] buffer = new byte[1024];

            while (isRunning && currentClient != null && currentClient.Connected)
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
                catch (Exception ex)
                {
                    LogMessage($"Receive error: {ex.Message}");
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
                            VirtualKeyCode key = parts[1] == "LEFT" ? VirtualKeyCode.LBUTTON : VirtualKeyCode.RBUTTON;
                            inputSimulator.Mouse.LeftButtonClick();
                        }
                        break;

                    case "MOUSE_DOWN":
                        if (parts.Length == 2)
                        {
                            if (parts[1] == "LEFT")
                                inputSimulator.Mouse.LeftButtonDown();
                            else
                                inputSimulator.Mouse.RightButtonDown();
                        }
                        break;

                    case "MOUSE_UP":
                        if (parts.Length == 2)
                        {
                            if (parts[1] == "LEFT")
                                inputSimulator.Mouse.LeftButtonUp();
                            else
                                inputSimulator.Mouse.RightButtonUp();
                        }
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

                    case "KEY_PRESS":
                        if (parts.Length == 2)
                        {
                            VirtualKeyCode key = (VirtualKeyCode)Enum.Parse(typeof(VirtualKeyCode), parts[1]);
                            inputSimulator.Keyboard.KeyPress(key);
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
            this.Invoke(new Action(() =>
            {
                txtLog.AppendText($"{DateTime.Now:HH:mm:ss} - {message}\n");
                txtLog.ScrollToCaret();
            }));
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
}