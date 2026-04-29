using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace WindowsFormsApp3
{
    public class ScoreEntry
    {
        public string Name { get; set; }
        public int Score { get; set; }
        public string Date { get; set; }
    }

    // Main game window class — inherits from Form (Windows window)
    public partial class Form1 : Form
    {
        private const int GridWidth = 30;           // number of grid columns
        private const int GridHeight = 25;          // number of grid rows
        private const int MineTickInterval = 40;    // how often (in ticks) mines are updated
        private const int MaxMines = 12;            // maximum number of mines on the board
        private const int MinSeg = 12;              // minimum cell size in pixels
        private const int BottomBar = 50;           // height of the bottom info bar
        private const int LbVisible = 9;            // number of leaderboard rows visible at once
        private const string Placeholder = "Your nickname..."; // placeholder text in the name field

        // readonly — value can only be assigned once
        // AppDomain.CurrentDomain.BaseDirectory — resolves the folder where the executable runs
        // Path.Combine — builds the full path to the scores file
        private static readonly string ScoresFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "snake_scores.txt");

        private int CellSize
        {
            get
            {
                int widthFactor = this.ClientSize.Width / GridWidth;
                int heightFactor = (this.ClientSize.Height - BottomBar) / GridHeight;
                return Math.Max(MinSeg, Math.Min(widthFactor, heightFactor));
            }
        }

        private int GameW
        {
            get { return GridWidth * CellSize; }
        }

        private int GameH
        {
            get { return GridHeight * CellSize; }
        }

        private List<Point> snake = new List<Point>(); // snake segments; [0] = head
        private Point direction = new Point(1, 0);      // current movement direction
        private Point nextDirection = new Point(1, 0);  // direction chosen by the player

        private Point food = new Point();
        private List<Point> mines = new List<Point>();
        private int tickCounter = 0;
        private int score = 0;
        private bool gameRunning = false;
        private Timer gameTimer = new Timer();     // timer that drives each game step
        private Random random = new Random();      // used for random position generation

        private List<ScoreEntry> leaderboard = new List<ScoreEntry>();
        private bool scoreSaved = false;
        private bool showLeaderboard = false;
        private int lbScroll = 0;                  // index of the first visible leaderboard row
        private string savedUniqueName = "";
        private bool confirmingZero = false;       // whether we are waiting for a zero-score confirmation

        private TextBox nameBox;
        private Button saveButton;
        private Button lbButton;
        private Button playAgainButton;
        private Label validationLabel;

        public Form1()
        {
            InitializeComponent(); // standard Windows Forms initialization

            this.Text = "Snake v2.0";
            this.ClientSize = new Size(GridWidth * 20, GridHeight * 20 + BottomBar);
            this.MinimumSize = new Size(GridWidth * MinSeg + 16, GridHeight * MinSeg + BottomBar + 39);
            this.FormBorderStyle = FormBorderStyle.FixedSingle; // window cannot be resized
            this.MaximizeBox = false;
            this.BackColor = Color.FromArgb(12, 12, 20); // creates a color from RGB components
            this.DoubleBuffered = true;  // eliminates screen flickering
            this.KeyPreview = true;      // window captures key events before controls do

            this.KeyDown += Form1_KeyDown;
            this.Paint += Form1_Paint;
            this.MouseWheel += Form1_MouseWheel;
            this.Resize += MyResizeMethod;
            gameTimer.Interval = 130;
            gameTimer.Tick += GameTimer_Tick;
            BuildUI();
            LoadLeaderboard();
            StartGame();
        }

        private void MyResizeMethod(object sender, EventArgs e)
        {
            // repositions all controls to fit the new window size
            UpdateLayout();

            // redraws the entire window
            this.Invalidate();
        }

        private void BuildUI()
        {
            // Text field for entering a nickname
            nameBox = new TextBox
            {
                Text = Placeholder,
                Font = new Font("Consolas", 11),
                Width = 190,
                Height = 30,
                BackColor = Color.FromArgb(30, 30, 45),
                ForeColor = Color.Gray,
                BorderStyle = BorderStyle.FixedSingle,
                MaxLength = 20,
                Visible = false
            };

            // clears the placeholder text when the field receives focus
            nameBox.GotFocus += RemovePlaceholder;

            void RemovePlaceholder(object sender, EventArgs e)
            {
                if (nameBox.Text == Placeholder)
                {
                    nameBox.Text = "";
                    nameBox.ForeColor = Color.White;
                }
            };

            // restores the placeholder when the field loses focus
            nameBox.LostFocus += RestorePlaceholder;

            void RestorePlaceholder(object sender, EventArgs e)
            {
                if (string.IsNullOrWhiteSpace(nameBox.Text))
                {
                    nameBox.Text = Placeholder;
                    nameBox.ForeColor = Color.Gray;
                }
            };

            // resets the save button state whenever the text changes
            nameBox.TextChanged += ResetFormStatus;

            void ResetFormStatus(object sender, EventArgs e)
            {
                confirmingZero = false;
                saveButton.Tag = null;
                saveButton.Text = "Save";
                validationLabel.Visible = false;
            };

            validationLabel = new Label
            {
                Text = "",
                Font = new Font("Consolas", 8, FontStyle.Bold),
                ForeColor = Color.OrangeRed,
                BackColor = Color.Transparent, // inherits the parent's background and becomes invisible
                AutoSize = true,
                Visible = false
            };

            saveButton = new Button
            {
                Text = "Save",
                Font = new Font("Consolas", 10, FontStyle.Bold),
                Width = 105,
                Height = 30,
                BackColor = Color.FromArgb(0, 130, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat, // renders the button without a 3-D border
                Visible = false
            };

            saveButton.FlatAppearance.BorderSize = 0;
            saveButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 170, 80); // hover background color
            saveButton.Click += SaveButton_Click;

            // Top 10 leaderboard button
            lbButton = new Button
            {
                Text = "Top 10",
                Font = new Font("Consolas", 10, FontStyle.Bold),
                Width = 120,
                Height = 30,
                BackColor = Color.FromArgb(160, 110, 0),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Visible = false
            };

            lbButton.FlatAppearance.BorderSize = 0;
            lbButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 145, 0);
            lbButton.Click += LeaderboardBtn_Click;

            playAgainButton = new Button
            {
                Text = "Play Again",
                Font = new Font("Consolas", 10, FontStyle.Bold),
                Width = 190,
                Height = 30,
                BackColor = Color.FromArgb(30, 40, 110),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Visible = false
            };

            playAgainButton.FlatAppearance.BorderSize = 0;
            playAgainButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 60, 150);
            playAgainButton.Click += ButtonClicked;

            void ButtonClicked(object sender, EventArgs e)
            {
                StartGame();
            }

            this.Controls.Add(nameBox);
            this.Controls.Add(validationLabel);
            this.Controls.Add(saveButton);
            this.Controls.Add(lbButton);
            this.Controls.Add(playAgainButton);

            UpdateLayout();
        }

        private void UpdateLayout()
        {
            int cx = GameW / 2;
            int cy = GameH / 2;

            if (showLeaderboard)
            {
                int panelHeight = 72 + LbVisible * 33 + 16;
                int belowY = 20 + panelHeight + 14; // vertical coordinate where buttons below the table start
                lbButton.Location = new Point(cx - 62, belowY);
                playAgainButton.Location = new Point(cx - 97, belowY + 40);

                nameBox.Visible = false;
                saveButton.Visible = false;
                validationLabel.Visible = false;
            }
            else
            {
                nameBox.Location = new Point(cx - 150, cy + 20);
                saveButton.Location = new Point(cx + 48, cy + 20);
                validationLabel.Location = new Point(cx - 150, cy + 54);
                lbButton.Location = new Point(cx - 62, cy + 64);
                playAgainButton.Location = new Point(cx - 97, cy + 104);
            }
        }

        private void LeaderboardBtn_Click(object sender, EventArgs e)
        {
            showLeaderboard = !showLeaderboard; // toggle leaderboard visibility
            lbScroll = 0;

            if (showLeaderboard)
            {
                lbButton.Text = "Back";
            }
            else
            {
                lbButton.Text = "Top 10";

                if (!scoreSaved)
                {
                    nameBox.Visible = true;
                    saveButton.Visible = true;
                }

                UpdateLayout();
                this.Invalidate();
            }

            if (showLeaderboard)
            {
                // If the score was saved, scroll to the player's position in the ranking
                if (scoreSaved && savedUniqueName != "")
                {
                    bool MatchEntry(ScoreEntry entry)
                    {
                        return entry.Name == savedUniqueName && entry.Score == score;
                    }

                    int playerIndex = leaderboard.FindIndex(MatchEntry);

                    if (playerIndex >= LbVisible)
                    {
                        lbScroll = playerIndex - LbVisible + 1;
                    }
                }
                else
                {
                    if (!scoreSaved)
                    {
                        nameBox.Visible = true;
                        saveButton.Visible = true;
                    }
                }

                UpdateLayout();
                this.Invalidate();
            }
        }

        private void ShowGameOverUI()
        {
            // Reset save state on every game over
            scoreSaved = false;
            showLeaderboard = false;
            confirmingZero = false;
            savedUniqueName = "";
            lbScroll = 0;

            saveButton.Enabled = true;
            saveButton.Text = "Save";
            saveButton.BackColor = Color.FromArgb(0, 130, 60);
            saveButton.Tag = null;
            lbButton.Text = "Top 10";

            nameBox.Text = Placeholder;
            nameBox.ForeColor = Color.Gray;
            validationLabel.Visible = false;

            nameBox.Visible = true;
            saveButton.Visible = true;
            lbButton.Visible = true;
            playAgainButton.Visible = true;

            UpdateLayout();
        }

        private void HideGameOverUI()
        {
            nameBox.Visible = false;
            saveButton.Visible = false;
            lbButton.Visible = false;
            playAgainButton.Visible = false;
            validationLabel.Visible = false;
        }

        private string ValidateName(string raw)
        {
            if (raw.Trim() == string.Empty || raw == Placeholder)
            {
                return "";
            }

            // Remove disallowed characters (file separator and newline characters)
            var sb = new System.Text.StringBuilder();

            foreach (char c in raw)
            {
                if (c != '|' && c != '\n' && c != '\r')
                {
                    sb.Append(c);
                }
            }

            string cleaned = sb.ToString().Trim();

            if (cleaned.Length < 2)
            {
                return "Nickname must be at least 2 characters long";
            }

            return ""; // no error
        }

        private string GetUniqueName(string baseName)
        {
            bool exists = false;

            foreach (ScoreEntry e in leaderboard)
            {
                if (e.Name == baseName)
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                return baseName;
            }

            // Find the highest existing numeric suffix (_2, _3, etc.)
            int maxSuffix = 1;

            foreach (ScoreEntry e in leaderboard)
            {
                if (e.Name.StartsWith(baseName + "_"))
                {
                    string tail = e.Name.Substring(baseName.Length + 1); // strip the base name prefix

                    // Check whether the remaining part is a valid number
                    if (int.TryParse(tail, out int n))
                    {
                        // Keep track of the highest suffix found
                        maxSuffix = Math.Max(maxSuffix, n);
                    }
                }
            }

            return baseName + "_" + (maxSuffix + 1);
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            if (scoreSaved)
            {
                return;
            }

            string raw = nameBox.Text.Trim();
            string name;

            if (raw.Trim() == "" || raw == Placeholder)
            {
                name = "Anonymous";
            }
            else
            {
                name = raw;
            }

            string errorMsg = ValidateName(raw);

            if (errorMsg != "")
            {
                validationLabel.Text = errorMsg;
                validationLabel.Visible = true;
                return;
            }

            string cleanName = "";

            foreach (char c in name)
            {
                if (c != '|' && c != '\n' && c != '\r')
                {
                    cleanName += c;
                }
            }

            cleanName = cleanName.Trim();

            if (cleanName.Length > 20)
            {
                cleanName = cleanName.Substring(0, 20); // truncate to 20 characters
            }

            name = cleanName;

            if (score == 0 && !confirmingZero)
            {
                confirmingZero = true;
                saveButton.Text = "Confirm";
                saveButton.BackColor = Color.FromArgb(150, 80, 0);
                validationLabel.Text = "Score is 0 — click again to save";
                validationLabel.Visible = true;
                return;
            }

            string uniqueName = GetUniqueName(name);
            savedUniqueName = uniqueName;

            ScoreEntry newEntry = new ScoreEntry();
            newEntry.Name = uniqueName;
            newEntry.Score = score;
            newEntry.Date = DateTime.Now.ToString("dd.MM.yyyy");
            leaderboard.Add(newEntry);

            int CompareResults(ScoreEntry a, ScoreEntry b)
            {
                if (b.Score > a.Score) return 1;
                if (b.Score < a.Score) return -1;
                return 0;
            }

            void RefreshRanking()
            {
                leaderboard.Sort(CompareResults);
                SaveLeaderboard();
            }

            RefreshRanking();

            scoreSaved = true;
            saveButton.Enabled = false;
            saveButton.Text = "Saved";
            saveButton.BackColor = Color.FromArgb(30, 30, 30);
            validationLabel.Visible = false;

            if (uniqueName != name)
            {
                validationLabel.Text = "Nickname taken — saved as: " + uniqueName;
                validationLabel.ForeColor = Color.LimeGreen;
                validationLabel.Visible = true;
            }

            nameBox.Text = uniqueName;
            nameBox.ForeColor = Color.LimeGreen;

            showLeaderboard = true;
            lbButton.Text = "Back";

            UpdateLayout();
            this.Invalidate();
        }

        private void SaveLeaderboard()
        {
            try
            {
                string FormatPlayerToFile(ScoreEntry x)
                {
                    return x.Name + "|" + x.Score + "|" + x.Date;
                }

                string[] lines = leaderboard.Take(500) // keep at most 500 entries
                    .Select(FormatPlayerToFile)
                    .ToArray();
                File.WriteAllLines(ScoresFile, lines);
            }
            catch
            {
                return;
            }
        }

        private void LoadLeaderboard()
        {
            leaderboard.Clear();

            if (!File.Exists(ScoresFile))
            {
                return;
            }

            try
            {
                foreach (string line in File.ReadAllLines(ScoresFile))
                {
                    string[] parts = line.Split('|');

                    if (parts.Length < 2)
                    {
                        continue;
                    }

                    // Checks whether the score field is a valid number; skips the line if not
                    if (int.TryParse(parts[1], out int s))
                    {
                        ScoreEntry entry = new ScoreEntry();
                        entry.Name = parts[0];
                        entry.Score = s; // assign the parsed score to the entry

                        if (parts.Length > 2)
                        {
                            entry.Date = parts[2];
                        }
                        else
                        {
                            entry.Date = "";
                        }

                        leaderboard.Add(entry);
                    }
                }

                void SortRanking(List<ScoreEntry> list)
                {
                    int n = list.Count;

                    for (int i = 0; i < n - 1; i++)
                    {
                        for (int j = 0; j < n - i - 1; j++)
                        {
                            if (list[j].Score < list[j + 1].Score)
                            {
                                var temp = list[j];
                                list[j] = list[j + 1];
                                list[j + 1] = temp;
                            }
                        }
                    }
                }

                SortRanking(leaderboard);
            }
            catch
            {
                return;
            }
        }

        private void StartGame()
        {
            HideGameOverUI();

            snake.Clear();
            mines.Clear();
            score = 0;
            tickCounter = 0;
            direction = new Point(1, 0);
            nextDirection = new Point(1, 0);
            gameTimer.Interval = 130; // game speed: one step every 130 ms

            // initial snake segments
            snake.Add(new Point(5, 12));
            snake.Add(new Point(4, 12));
            snake.Add(new Point(3, 12));

            SpawnFood();
            gameRunning = true;
            gameTimer.Start();
            this.Invalidate();
        }

        private void SpawnFood()
        {
            do
            {
                food = new Point(random.Next(0, GridWidth), random.Next(0, GridHeight));
            }
            while (snake.Contains(food) || mines.Contains(food)); // ensure food does not overlap the snake or a mine
        }

        private void SpawnMine()
        {
            // try up to 100 times to find a valid mine position
            for (int attempt = 0; attempt < 100; attempt++)
            {
                Point p = new Point(random.Next(0, GridWidth), random.Next(0, GridHeight));

                if (snake.Contains(p) || p == food || mines.Contains(p))
                {
                    continue;
                }

                // skip cells that are too close to the snake's head
                int dist = Math.Abs(p.X - snake[0].X) + Math.Abs(p.Y - snake[0].Y);

                if (dist < 4)
                {
                    continue;
                }

                mines.Add(p);
                break;
            }
        }

        private void UpdateMines()
        {
            if (mines.Count < MaxMines)
            {
                SpawnMine();
            }
            else
            {
                int indexToRemove = random.Next(mines.Count); // remove a randomly chosen mine
                mines.RemoveAt(indexToRemove);
                SpawnMine();
            }
        }

        private void GameTimer_Tick(object sender, EventArgs e)
        {
            if (!gameRunning)
            {
                return;
            }

            direction = nextDirection;
            tickCounter++;

            if (tickCounter % MineTickInterval == 0)
            {
                UpdateMines();
            }

            Point newHead = new Point(snake[0].X + direction.X, snake[0].Y + direction.Y); // compute the new head position

            // collision detection
            bool hitWall = newHead.X < 0 || newHead.X >= GridWidth || newHead.Y < 0 || newHead.Y >= GridHeight;
            bool hitSelf = snake.Contains(newHead);
            bool hitMine = mines.Contains(newHead);

            if (hitWall || hitSelf || hitMine)
            {
                GameOver();
                return;
            }

            snake.Insert(0, newHead); // insert the new head

            if (newHead == food)
            {
                score += 10;
                SpawnFood();

                if (score % 50 == 0 && gameTimer.Interval > 60)
                {
                    gameTimer.Interval -= 10;
                }
            }
            else
            {
                snake.RemoveAt(snake.Count - 1);
            }

            this.Invalidate();
        }

        private void GameOver()
        {
            gameRunning = false;
            gameTimer.Stop();
            ShowGameOverUI();
            this.Invalidate();
        }

        private void Form1_MouseWheel(object sender, MouseEventArgs e)
        {
            if (!showLeaderboard || gameRunning)
            {
                return;
            }

            int max = Math.Max(0, leaderboard.Count - LbVisible); // maximum scroll depth

            int delta;
            if (e.Delta > 0)
            {
                delta = -1; // scroll up
            }
            else
            {
                delta = 1;  // scroll down
            }

            lbScroll = Math.Max(0, Math.Min(lbScroll + delta, max)); // clamp scroll position
            this.Invalidate();
        }

        private void Form1_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics; // graphics context for drawing
            int seg = CellSize;      // cell size in pixels
            g.SmoothingMode = SmoothingMode.AntiAlias; // enables anti-aliasing

            // draw the background grid
            using (Pen gridPen = new Pen(Color.FromArgb(25, 255, 255, 255)))
            {
                for (int x = 0; x <= GridWidth; x++) // vertical lines
                {
                    g.DrawLine(gridPen, x * seg, 0, x * seg, GameH);
                }
                for (int y = 0; y <= GridHeight; y++) // horizontal lines
                {
                    g.DrawLine(gridPen, 0, y * seg, GameW, y * seg);
                }
            }

            DrawMines(g, seg);

            for (int i = 0; i < snake.Count; i++)
            {
                Color c;

                if (i == 0)
                {
                    c = Color.LimeGreen; // brighter color for the head
                }
                else
                {
                    c = Color.FromArgb(0, 175, 0); // darker color for the body
                }

                using (SolidBrush b = new SolidBrush(c))
                {
                    g.FillRectangle(b, snake[i].X * seg + 1, snake[i].Y * seg + 1, seg - 2, seg - 2);
                }
            }

            int fr = Math.Max(3, seg / 2 - 3); // compute food radius so it remains visible at any cell size
            g.FillEllipse(Brushes.Crimson, food.X * seg + seg / 2 - fr, food.Y * seg + seg / 2 - fr, fr * 2, fr * 2); // draw food

            g.SmoothingMode = SmoothingMode.Default; // restore default rendering mode
            int barY = GameH;

            // draw the bottom info bar
            using (var barBrush = new LinearGradientBrush(
                new Rectangle(0, barY, this.ClientSize.Width, BottomBar),
                Color.FromArgb(20, 20, 35), Color.FromArgb(10, 10, 20),
                LinearGradientMode.Vertical))
            {
                g.FillRectangle(barBrush, 0, barY, this.ClientSize.Width, BottomBar);
            }

            using (var separatorPen = new Pen(Color.FromArgb(60, 60, 100)))
            {
                g.DrawLine(separatorPen, 0, barY, this.ClientSize.Width, barY); // separator line between game area and bar
            }

            g.DrawString("Score: " + score + "   Length: " + snake.Count + "   Mines: " + mines.Count + "/" + MaxMines + "   [ENTER] restart", new Font("Consolas", 9), Brushes.White, 10, barY + 17);

            if (!gameRunning)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;

                if (showLeaderboard)
                {
                    DrawLeaderboardOverlay(g);
                }
                else
                {
                    DrawGameOverOverlay(g);
                }
            }
        }

        private void DrawGameOverOverlay(Graphics g)
        {
            int cx = GameW / 2;
            int cy = GameH / 2;

            using (var bg = new LinearGradientBrush(new Rectangle(0, 0, GameW, GameH), Color.FromArgb(215, 8, 5, 18), Color.FromArgb(215, 3, 3, 12), LinearGradientMode.Vertical))
            {
                g.FillRectangle(bg, 0, 0, GameW, GameH);
            }

            int boxX = 50, boxW = GameW - 100;
            int boxY = cy - 145, boxH = 310;

            using (var pen1 = new Pen(Color.FromArgb(140, 200, 60, 0), 1.5f))
            {
                g.DrawRectangle(pen1, boxX, boxY, boxW, boxH); // inner border frame
            }

            using (var pen2 = new Pen(Color.FromArgb(50, 255, 100, 0), 6))
            {
                g.DrawRectangle(pen2, boxX - 1, boxY - 1, boxW + 2, boxH + 2); // outer glow frame
            }

            using (Font fBig = new Font("Consolas", 24, FontStyle.Bold)) // main heading
            using (Font fMid = new Font("Consolas", 13))                  // sub-heading
            using (Font fSm = new Font("Consolas", 9))                    // small info text
            {
                string title = "  GAME OVER  ";

                SizeF titleSz = g.MeasureString(title, fBig);
                g.DrawString(title, fBig, Brushes.OrangeRed, cx - titleSz.Width / 2, cy - 125);

                string scoreStr = "Final score:  " + score + "  pts";

                SizeF scoreSz = g.MeasureString(scoreStr, fMid);
                g.DrawString(scoreStr, fMid, Brushes.White, cx - scoreSz.Width / 2, cy - 82);

                int pos = 0;

                if (scoreSaved && savedUniqueName != "")
                {
                    bool IsThisMyResult(ScoreEntry entry)
                    {
                        return entry.Name == savedUniqueName && entry.Score == score;
                    }

                    pos = leaderboard.FindIndex(IsThisMyResult) + 1;
                }

                if (pos > 0)
                {
                    Brush rankColor;
                    bool disposeBrush = false;

                    if (pos == 1)
                    {
                        rankColor = Brushes.Gold;
                    }
                    else if (pos == 2)
                    {
                        rankColor = Brushes.Silver;
                    }
                    else if (pos == 3)
                    {
                        rankColor = new SolidBrush(Color.FromArgb(205, 127, 50)); // bronze color
                        disposeBrush = true;
                    }
                    else
                    {
                        rankColor = Brushes.LightCyan;
                    }

                    string rankStr;

                    if (pos <= 3)
                    {
                        rankStr = "#" + pos + " place on the leaderboard!";
                    }
                    else
                    {
                        rankStr = "\nYou are ranked #" + pos + " on the leaderboard";
                    }

                    SizeF rankSz = g.MeasureString(rankStr, fMid);
                    g.DrawString(rankStr, fMid, rankColor, cx - rankSz.Width / 2, cy - 50);

                    if (disposeBrush)
                    {
                        rankColor.Dispose();
                    }
                }
            }
        }

        private void DrawLeaderboardOverlay(Graphics g)
        {
            int gw = GameW;
            int gh = GameH;
            int cx = gw / 2;

            using (var bg = new LinearGradientBrush(new Rectangle(0, 0, gw, gh), Color.FromArgb(235, 5, 5, 22), Color.FromArgb(235, 8, 5, 35), LinearGradientMode.Vertical))
            {
                g.FillRectangle(bg, 0, 0, gw, gh);
            }

            int panelW = Math.Min(560, gw - 30); // panel is 560 px wide, or window width minus 30 px if the window is too narrow
            int rowH = 33;
            int panelX = cx - panelW / 2;
            int panelY = 20;
            int panelH = 72 + LbVisible * rowH + 16;

            using (var shadow = new SolidBrush(Color.FromArgb(80, 0, 0, 0)))
            {
                g.FillRectangle(shadow, panelX + 5, panelY + 5, panelW, panelH);
            }

            using (var panBg = new LinearGradientBrush(new Rectangle(panelX, panelY, panelW, panelH), Color.FromArgb(210, 12, 10, 32), Color.FromArgb(210, 8, 7, 22), LinearGradientMode.Vertical))
            {
                g.FillRectangle(panBg, panelX, panelY, panelW, panelH);
            }

            using (var outerPen = new Pen(Color.FromArgb(60, 220, 160, 0), 6))
            {
                g.DrawRectangle(outerPen, panelX, panelY, panelW, panelH);
            }

            using (var innerPen = new Pen(Color.FromArgb(200, 220, 160, 0), 1.5f))
            {
                g.DrawRectangle(innerPen, panelX + 3, panelY + 3, panelW - 6, panelH - 6);
            }

            int firstRowY = panelY + 70;

            using (Font fTitle = new Font("Consolas", 15, FontStyle.Bold)) // table title
            using (Font fSub = new Font("Consolas", 8))                    // small subtitle
            using (Font fRow = new Font("Consolas", 10, FontStyle.Bold))   // main row text
            using (Font fRowS = new Font("Consolas", 9))                   // supplementary row text
            {
                string title = "HALL OF FAME";

                SizeF titleSz = g.MeasureString(title, fTitle);
                g.DrawString(title, fTitle, Brushes.Gold, cx - titleSz.Width / 2, panelY + 10);

                int lineY = panelY + 42;

                using (var lp = new Pen(Color.FromArgb(120, 200, 140, 0), 1))
                {
                    g.DrawLine(lp, panelX + 12, lineY, panelX + panelW - 12, lineY);
                }

                string sub = leaderboard.Count + " entries  [scroll] to navigate";
                SizeF subSz = g.MeasureString(sub, fSub);

                using (var brush = new SolidBrush(Color.FromArgb(110, 180, 180, 180)))
                {
                    g.DrawString(sub, fSub, brush, cx - subSz.Width / 2, panelY + 48);
                }

                var visible = leaderboard.Skip(lbScroll).Take(LbVisible).ToList(); // Skip() — skip entries above the scroll position

                if (leaderboard.Count == 0)
                {
                    string empty = "No scores yet";

                    SizeF emptySz = g.MeasureString(empty, fRowS);

                    using (var brush = new SolidBrush(Color.FromArgb(120, 200, 200, 200)))
                    {
                        g.DrawString(empty, fRowS, brush, cx - emptySz.Width / 2, firstRowY + 20);
                    }
                }

                for (int i = 0; i < visible.Count; i++)
                {
                    int globalIdx = i + lbScroll;
                    int rowY = firstRowY + i * rowH;
                    ScoreEntry entry = visible[i];

                    bool isMe = scoreSaved && entry.Name == savedUniqueName && entry.Score == score;

                    if (isMe)
                    {
                        using (var hb = new SolidBrush(Color.FromArgb(55, 255, 220, 0)))
                        {
                            g.FillRectangle(hb, panelX + 4, rowY, panelW - 8, rowH - 1);
                        }

                        using (var hp = new Pen(Color.FromArgb(100, 255, 200, 0), 1))
                        {
                            g.DrawRectangle(hp, panelX + 4, rowY, panelW - 9, rowH - 2);
                        }
                    }
                    else if (i % 2 == 0)
                    {
                        using (var zebraBrush = new SolidBrush(Color.FromArgb(15, 255, 255, 255)))
                        {
                            g.FillRectangle(zebraBrush, panelX + 4, rowY, panelW - 8, rowH - 1);
                        }
                    }

                    Color rowColor;

                    if (globalIdx == 0)
                    {
                        rowColor = Color.Gold;
                    }
                    else if (globalIdx == 1)
                    {
                        rowColor = Color.Silver;
                    }
                    else if (globalIdx == 2)
                    {
                        rowColor = Color.FromArgb(210, 140, 60);
                    }
                    else if (isMe)
                    {
                        rowColor = Color.Yellow;
                    }
                    else
                    {
                        rowColor = Color.FromArgb(200, 200, 210);
                    }

                    string medal;

                    if (globalIdx == 0)
                    {
                        medal = "1.";
                    }
                    else if (globalIdx == 1)
                    {
                        medal = "2.";
                    }
                    else if (globalIdx == 2)
                    {
                        medal = "3.";
                    }
                    else
                    {
                        medal = (globalIdx + 1) + ".";
                    }

                    string displayName;

                    if (entry.Name.Length > 18)
                    {
                        displayName = entry.Name.Substring(0, 17) + "~";
                    }
                    else
                    {
                        displayName = entry.Name;
                    }

                    string pts = entry.Score.ToString().PadLeft(7); // right-align score
                    string date = entry.Date;

                    int col1 = panelX + 8;
                    int col2 = panelX + 55;
                    int col3 = panelX + panelW - 200;
                    int col4 = panelX + panelW - 95;

                    using (SolidBrush rb = new SolidBrush(rowColor))
                    {
                        g.DrawString(medal, fRow, rb, col1, rowY + 5);
                        g.DrawString(displayName, fRow, rb, col2, rowY + 5);
                        g.DrawString(pts, fRow, rb, col3, rowY + 5);
                    }

                    using (var brush = new SolidBrush(Color.FromArgb(110, 180, 180, 180)))
                    {
                        g.DrawString("pts", fRowS, brush, col3 + 56, rowY + 7);
                    }

                    using (var brush = new SolidBrush(Color.FromArgb(100, 170, 170, 170)))
                    {
                        g.DrawString(date, fRowS, brush, col4, rowY + 7);
                    }

                    if (isMe)
                    {
                        using (var brush = new SolidBrush(Color.FromArgb(200, 255, 220, 0)))
                        {
                            g.DrawString("<", fRowS, brush, panelX + panelW - 18, rowY + 7);
                        }
                    }
                }
            }

            if (leaderboard.Count > LbVisible)
            {
                int sbX = panelX + panelW - 5;
                int sbTopY = firstRowY;
                int sbH = LbVisible * rowH;
                int thumbH = Math.Max(18, sbH * LbVisible / leaderboard.Count);
                int maxScr = leaderboard.Count - LbVisible;

                int thumbY;

                if (maxScr > 0)
                {
                    thumbY = sbTopY + (lbScroll * (sbH - thumbH) / maxScr);
                }
                else
                {
                    thumbY = sbTopY;
                }

                using (var brush = new SolidBrush(Color.FromArgb(30, 255, 255, 255)))
                {
                    g.FillRectangle(brush, sbX, sbTopY, 4, sbH); // scrollbar track
                }

                using (var thumbBrush = new LinearGradientBrush(new Rectangle(sbX, thumbY, 4, thumbH), Color.Gold, Color.FromArgb(180, 140, 0), LinearGradientMode.Vertical)) // scrollbar thumb
                {
                    g.FillRectangle(thumbBrush, sbX, thumbY, 4, thumbH);
                }
            }
        }

        private void DrawMines(Graphics g, int seg)
        {
            foreach (Point m in mines)
            {
                int cx = m.X * seg + seg / 2;
                int cy = m.Y * seg + seg / 2;
                int r = Math.Max(4, seg / 2 - 3); // mine radius

                using (var brush = new SolidBrush(Color.FromArgb(55, 55, 55)))
                {
                    g.FillEllipse(brush, cx - r, cy - r, r * 2, r * 2); // draw mine body
                }

                using (var outlinePen = new Pen(Color.FromArgb(210, 210, 210), 1.5f))
                {
                    g.DrawEllipse(outlinePen, cx - r, cy - r, r * 2, r * 2);
                }

                using (var fusePen = new Pen(Color.Gray, 2))
                {
                    g.DrawLine(fusePen, cx, cy - r, cx, cy - r - 4);
                }

                int[] dx = { 0, 0, 1, -1, 1, -1, 1, -1 };
                int[] dy = { 1, -1, 0, 0, 1, 1, -1, -1 };

                // draw mine spikes
                using (Pen spikePen = new Pen(Color.FromArgb(175, 175, 175), 1.5f))
                {
                    for (int i = 0; i < 8; i++)
                    {
                        g.DrawLine(spikePen, cx + dx[i] * r, cy + dy[i] * r, cx + dx[i] * (r + 3), cy + dy[i] * (r + 3));
                    }
                }

                using (var brush = new SolidBrush(Color.FromArgb(70, 255, 255, 255)))
                {
                    g.FillEllipse(brush, cx - r / 2, cy - r / 2, r / 2, r / 2); // specular highlight
                }
            }
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (!gameRunning && showLeaderboard && (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down)) // scroll leaderboard with arrow keys
            {
                int max = Math.Max(0, leaderboard.Count - LbVisible);

                if (e.KeyCode == Keys.Down)
                {
                    lbScroll++;
                }
                else
                {
                    lbScroll--;
                }

                lbScroll = Math.Max(0, Math.Min(lbScroll, max));
                this.Invalidate();
                return;
            }

            switch (e.KeyCode)
            {
                case Keys.Up:
                case Keys.W:
                    if (gameRunning && direction.Y != 1)
                    {
                        nextDirection = new Point(0, -1);
                    }
                    break;

                case Keys.Down:
                case Keys.S:
                    if (gameRunning && direction.Y != -1)
                    {
                        nextDirection = new Point(0, 1);
                    }
                    break;

                case Keys.Left:
                case Keys.A:
                    if (gameRunning && direction.X != 1)
                    {
                        nextDirection = new Point(-1, 0);
                    }
                    break;

                case Keys.Right:
                case Keys.D:
                    if (gameRunning && direction.X != -1)
                    {
                        nextDirection = new Point(1, 0);
                    }
                    break;

                case Keys.Enter:
                    if (!gameRunning && !nameBox.Focused)
                    {
                        StartGame();
                    }
                    break;

                case Keys.Escape:
                    if (!gameRunning && showLeaderboard)
                    {
                        showLeaderboard = false;
                        lbButton.Text = "Top 10";
                        UpdateLayout();
                        this.Invalidate();
                    }
                    break;
            }
        }
    }
}