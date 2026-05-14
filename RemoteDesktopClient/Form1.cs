/*

using System;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;
using WindowsInput;

namespace RemoteDesktopClient
{
    public partial class Form1 : Form
    {
        private TcpClient tcpClient;
        private NetworkStream networkStream;
        private bool isConnected = false;
        private Thread receiveThread;

        // UI Components
        private TextBox txtServerIP;
        private TextBox txtPort;
        private Button btnConnect;
        private Label lblStatus;
        private PictureBox pictureBoxRemoteScreen;
        private Button btnDisconnect;
        private RichTextBox txtLog;
        private CheckBox chkControlEnabled;

        // Mouse position tracking for relative movement
        private Point lastMousePos;
        private bool isMouseDown = false;

        public Form1()
        {
            InitializeComponent();
            SetupUI();
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

            lblStatus = new Label() { Text = "Status: Disconnected", Location = new Point(495, 15), Width = 150 };

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
            try
            {
                tcpClient = new TcpClient();
                tcpClient.Connect(txtServerIP.Text, int.Parse(txtPort.Text));
                networkStream = tcpClient.GetStream();
                isConnected = true;

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
                MessageBox.Show($"Connection failed: {ex.Message}");
                LogMessage($"Connection error: {ex.Message}");
            }
        }

        private void BtnDisconnect_Click(object sender, EventArgs e)
        {
            Disconnect();
        }

        private void Disconnect()
        {
            isConnected = false;
            tcpClient?.Close();
            networkStream?.Close();
            receiveThread?.Join(500);

            btnConnect.Enabled = true;
            btnDisconnect.Enabled = false;
            txtServerIP.Enabled = true;
            txtPort.Enabled = true;
            lblStatus.Text = "Status: Disconnected";
            lblStatus.ForeColor = Color.Red;
            pictureBoxRemoteScreen.Image = null;

            LogMessage("Disconnected from server");
        }

        private void ReceiveScreen()
        {
            byte[] sizeBuffer = new byte[4];

            while (isConnected && tcpClient.Connected)
            {
                try
                {
                    // Read image size (4 bytes)
                    int bytesRead = 0;
                    while (bytesRead < 4)
                    {
                        bytesRead += networkStream.Read(sizeBuffer, bytesRead, 4 - bytesRead);
                    }

                    int imageSize = BitConverter.ToInt32(sizeBuffer, 0);

                    // Read image data
                    byte[] imageData = new byte[imageSize];
                    bytesRead = 0;
                    while (bytesRead < imageSize)
                    {
                        bytesRead += networkStream.Read(imageData, bytesRead, imageSize - bytesRead);
                    }

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
                catch (Exception ex)
                {
                    if (isConnected)
                        LogMessage($"Receive error: {ex.Message}");
                    break;
                }
            }
        }

        private void SendCommand(string command)
        {
            if (!isConnected || !chkControlEnabled.Checked) return;

            try
            {
                byte[] data = System.Text.Encoding.ASCII.GetBytes(command);
                networkStream.Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                LogMessage($"Send error: {ex.Message}");
            }
        }

        private void PictureBoxRemoteScreen_MouseMove(object sender, MouseEventArgs e)
        {
            if (!chkControlEnabled.Checked) return;

            // Calculate absolute position based on actual screen size
            if (pictureBoxRemoteScreen.Image != null)
            {
                float scaleX = (float)Screen.PrimaryScreen.Bounds.Width / pictureBoxRemoteScreen.Image.Width;
                float scaleY = (float)Screen.PrimaryScreen.Bounds.Height / pictureBoxRemoteScreen.Image.Height;

                int actualX = (int)(e.X * scaleX);
                int actualY = (int)(e.Y * scaleY);

                SendCommand($"MOUSE_MOVE|{actualX}|{actualY}");
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

            // Send as key press for characters
            if (e.KeyValue >= 65 && e.KeyValue <= 90) // A-Z
            {
                SendCommand($"KEY_PRESS|{keyCode}");
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
            Disconnect();
        }
    }
}*/



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
}