using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.IO;
using System.Windows.Media.Imaging;

namespace ShootingGame
{
    public static class Leaderboard
    {
        private const string FilePath = "leaderboard.txt";

        // Чтение данных из файла
        public static List<(string Name, double Score)> LoadLeaderboard()
        {
            if (!File.Exists(FilePath))
                return new List<(string Name, double Score)>();

            return File.ReadAllLines(FilePath)
                .Select(line => line.Split(';'))
                .Where(parts => parts.Length == 2)
                .Select(parts => (Name: parts[0], Score: double.Parse(parts[1])))
                .OrderByDescending(entry => entry.Score)
                .Take(10)
                .ToList();
        }

        // Сохранение данных в файл
        public static void SaveLeaderboard(List<(string Name, double Score)> leaderboard)
        {
            var lines = leaderboard
                .Select(entry => $"{entry.Name};{entry.Score}")
                .ToArray();
            File.WriteAllLines(FilePath, lines);
        }

        // Добавление нового результата
        public static void AddScore(string name, double score)
        {
            // Загружаем текущую таблицу лидеров
            var leaderboard = LoadLeaderboard();

            // Ищем запись с тем же именем
            var existingEntry = leaderboard.FirstOrDefault(entry => entry.Name == name);

            if (existingEntry.Name != null)
            {
                // Если результат нового игрока больше, обновляем его запись
                if (score > existingEntry.Score)
                {
                    leaderboard.Remove(existingEntry);
                    leaderboard.Add((name, score));
                }
            }
            else
            {
                // Если имени нет, добавляем новую запись
                leaderboard.Add((name, score));
            }

            // Оставляем только топ-10 записей
            leaderboard = leaderboard.OrderByDescending(entry => entry.Score).Take(10).ToList();

            // Сохраняем таблицу лидеров
            SaveLeaderboard(leaderboard);
        }
    }

    public class Enemy
    {
        public Ellipse Shape { get; set; }
        public double SpeedX { get; set; }
        public double SpeedY { get; set; }
        public int Type { get; set; }
    }

    public partial class MainWindow : Window
    {
        

        private MediaPlayer mediaPlayer;
        private bool isPaused = false;
        private double score = 0;
        private Random random = new Random();
        private List<Enemy> enemies = new List<Enemy>();
        private Stopwatch stopwatch;
        private const double TargetFrameTime = 1000.0 / 144.0; // Время кадра для 144 FPS
        private double enemySpeedMultiplier = 1.0; // Коэффициент сложности


        public MainWindow()
        {
            InitializeComponent();

            this.Cursor = Cursors.Cross;

            // Инициализируем MediaPlayer и включаем зацикливание
            mediaPlayer = new MediaPlayer();
            mediaPlayer.Open(new Uri("music.mp3", UriKind.Relative)); // Путь к файлу музыки
            mediaPlayer.Play(); // Начинаем воспроизведение

            mediaPlayer.MediaEnded += MediaPlayer_MediaEnded;
        }

        // Метод, который будет вызываться при окончании воспроизведения музыки
        private void MediaPlayer_MediaEnded(object sender, EventArgs e)
        {
            // Перезапускаем музыку
            mediaPlayer.Position = TimeSpan.Zero; // Сбрасываем позицию
            mediaPlayer.Play(); // Снова начинаем воспроизведение
        }

        private void ChooseDiffficult_Click(object sender, RoutedEventArgs e)
        {
            MainMenu.Visibility = Visibility.Hidden;
            DifficultMenu.Visibility = Visibility.Visible;

        }

        private void StartGameEasy_Click(object sender, RoutedEventArgs e)
        {
            enemySpeedMultiplier = 0.5; // Обычная сложность
            StartGame();
        }

        private void StartGameHard_Click(object sender, RoutedEventArgs e)
        {
            enemySpeedMultiplier = 1.5; // Высокая сложность
            StartGame();
        }
        
        private void StartGameNormal_Click(object sender, RoutedEventArgs e)
        {
            enemySpeedMultiplier = 1.0; // Высокая сложность
            StartGame();
        }

        private void ShowInfo_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Добро пожаловать в игру! Цель игры: сбивать врагов, кликая по ним. Выберите уровень сложности и начните играть. Будьте осторожны, если метеоритов будет слишком много, вы проиграли! Удачи!", "Информация");
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void StartGame()
        {
            MainMenu.Visibility = Visibility.Hidden;
            DifficultMenu.Visibility = Visibility.Hidden;
            GameCanvas.Visibility = Visibility.Visible;

            InitializeGame();
            isPaused = false; // Сбрасываем состояние паузы

            // Музыка продолжает играть без остановки
            if (isPaused)
                mediaPlayer.Pause();
            else
                mediaPlayer.Play();
        }



        private void InitializeGame()
        {
            // Установить курсор мыши на перекрестие
            this.Cursor = Cursors.Cross;

            // Сброс очков
            score = 0;
            ScoreText.Text = $"Очки: {score}";

            // Очистить игровое поле
            GameCanvas.Children.Clear();

            // Добавляем текст и кнопки после всех противников
            GameCanvas.Children.Add(ScoreText);
            GameCanvas.Children.Add(PauseButton);
            GameCanvas.Children.Add(ExitButton);

            // Убедитесь, что кнопки будут всегда вверху
            Canvas.SetZIndex(PauseButton, 1);
            Canvas.SetZIndex(ExitButton, 1);
            Canvas.SetZIndex(ScoreText, 1);

            // Обработка кликов мыши
            GameCanvas.MouseLeftButtonDown += Shoot;

            // Инициализация таймера
            stopwatch = new Stopwatch();
            stopwatch.Start();

            CompositionTarget.Rendering += GameLoop;
        }


        private void SpawnEnemy()
        {
            if (isPaused) return;

            // Выбираем случайный тип врага
            int[] enemyTypes = { 1, 2, 3 };
            int enemyType = enemyTypes[random.Next(enemyTypes.Length)];

            // Настроим размер и изображение в зависимости от типа
            Ellipse enemyShape = new Ellipse();
            double size = 0;
            string imageUri = "";

            switch (enemyType)
            {
                case 1:
                    size = 40;
                    imageUri = "small_enemy.png"; // Путь к картинке для маленького врага
                    break;
                case 2:
                    size = 80;
                    imageUri = "medium_enemy.png"; // Путь к картинке для среднего врага
                    break;
                case 3:
                    size = 100;
                    imageUri = "large_enemy.png"; // Путь к картинке для большого врага
                    break;
            }

            // Устанавливаем размеры врага
            enemyShape.Width = size;
            enemyShape.Height = size;

            // Загружаем картинку для врага
            enemyShape.Fill = new ImageBrush
            {
                ImageSource = new BitmapImage(new Uri(imageUri, UriKind.Relative)) // Путь к картинке
            };

            double startX = random.Next(0, (int)(GameCanvas.ActualWidth - enemyShape.Width));
            double startY = random.Next(50, (int)(GameCanvas.ActualHeight - enemyShape.Height));
            Canvas.SetLeft(enemyShape, startX);
            Canvas.SetTop(enemyShape, startY);

            double speedX = (random.NextDouble() * 4 - 2) * enemySpeedMultiplier; // С учётом сложности
            double speedY = (random.NextDouble() * 4 - 2) * enemySpeedMultiplier;

            GameCanvas.Children.Add(enemyShape);
            enemies.Add(new Enemy { Shape = enemyShape, SpeedX = speedX, SpeedY = speedY, Type = enemyType });
        }

        private void GameLoop(object sender, EventArgs e)
        {
            if (isPaused) return;

            double elapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds;

            // Проверяем, если на экране больше 10 противников
            if (enemies.Count > 15)
            {
                EndGame();
                return;
            }

            if (elapsedMilliseconds >= TargetFrameTime)
            {
                UpdateEnemies();

                // Убедитесь, что враги начинают появляться не сразу
                if (random.Next((int)(50 / enemySpeedMultiplier)) == 0)
                {
                    SpawnEnemy();
                }

                stopwatch.Restart();
            }
        }

        private void EndGame()
        {
            // Завершаем игру, показываем сообщение
            isPaused = true;
            MessageBox.Show("Игра завершена! Слишком много астероидов.", "Конец игры");

            // Показываем таблицу рекордов
            string playerName = GetPlayerName();
            Leaderboard.AddScore(playerName, score);

            // Возвращаем в главное меню
            GameCanvas.Visibility = Visibility.Hidden;
            MainMenu.Visibility = Visibility.Visible;
            this.Cursor = Cursors.Cross;

            // Сбросить состояние игры
            ExitButton.Visibility = Visibility.Collapsed;
            stopwatch.Reset();  // Остановить таймер
            enemies.Clear();  // Очистить список врагов
        }

        private void UpdateEnemies()
        {
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                Enemy enemy = enemies[i];
                double left = Canvas.GetLeft(enemy.Shape);
                double top = Canvas.GetTop(enemy.Shape);

                enemy.SpeedX += random.NextDouble() / 75;
                enemy.SpeedY += random.NextDouble() / 75;

                double newLeft = left + enemy.SpeedX;
                double newTop = top + enemy.SpeedY;

                if (newLeft <= 0 || newLeft >= GameCanvas.ActualWidth - enemy.Shape.Width)
                    enemy.SpeedX = -enemy.SpeedX;
                if (newTop <= 0 || newTop >= GameCanvas.ActualHeight - enemy.Shape.Height)
                    enemy.SpeedY = -enemy.SpeedY;

                Canvas.SetLeft(enemy.Shape, newLeft);
                Canvas.SetTop(enemy.Shape, newTop);
            }
        }

        private void Shoot(object sender, MouseButtonEventArgs e)
        {
            if (isPaused) return;

            Point clickPosition = e.GetPosition(GameCanvas);

            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                Enemy enemy = enemies[i];
                double left = Canvas.GetLeft(enemy.Shape);
                double top = Canvas.GetTop(enemy.Shape);
                
                if (clickPosition.X >= left && clickPosition.X <= left + enemy.Shape.Width &&
                    clickPosition.Y >= top && clickPosition.Y <= top + enemy.Shape.Height)
                {
                    GameCanvas.Children.Remove(enemy.Shape);
                    enemies.RemoveAt(i);

                    score += 10 * enemySpeedMultiplier - enemy.Type;
                    ScoreText.Text = $"Очки: {score}";
                }
            }
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            isPaused = !isPaused;
            PauseButton.Content = isPaused ? "Продолжить" : "Пауза";

            // Показать или скрыть кнопку выхода
            ExitButton.Visibility = isPaused ? Visibility.Visible : Visibility.Collapsed;

            // Обновить интерфейс
            GameCanvas.UpdateLayout();
        }

        private void ExitToMainMenu_Click(object sender, RoutedEventArgs e)
        {
            string playerName = GetPlayerName();
            Leaderboard.AddScore(playerName, score);

            MessageBox.Show($"Ваш счет: {score}", "Игра окончена");

            // Вернуться в главное меню
            GameCanvas.Visibility = Visibility.Hidden;
            MainMenu.Visibility = Visibility.Visible;

            // Сброс состояния игры
            isPaused = false;
            ExitButton.Visibility = Visibility.Collapsed;
            stopwatch.Reset();  // Остановить таймер
            enemies.Clear();  // Очистить список врагов
        }

        private void ShowLeaderboard_Click(object sender, RoutedEventArgs e)
        {
            var leaderboard = Leaderboard.LoadLeaderboard();

            string leaderboardText = string.Join("\n", leaderboard
                .Select((entry, index) => $"{index + 1}. {entry.Name} - {(int)entry.Score:F2}"));

            MessageBox.Show($"Таблица рекордов:\n{leaderboardText}", "Таблица рекордов");
        }

        private string GetPlayerName()
        {
            // Создаем окно для ввода имени
            Window inputWindow = new Window
            {
                Title = "Введите ваше имя",
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
                WindowStyle = WindowStyle.ToolWindow
            };

            // Создаем основной контейнер
            Grid grid = new Grid { Margin = new Thickness(10) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Текстовое сообщение
            TextBlock prompt = new TextBlock
            {
                Text = "Введите ваше имя:",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 20)
            };
            Grid.SetRow(prompt, 0);

            // Поле ввода текста
            TextBox inputBox = new TextBox
            {
                FontSize = 14,
                Height = 30,
                Margin = new Thickness(0, 0, 0, 20)
            };
            Grid.SetRow(inputBox, 1);

            // Кнопка подтверждения
            Button confirmButton = new Button
            {
                Content = "OK",
                FontSize = 14,
                Width = 100,
                Height = 30,
                Margin = new Thickness(0, 10, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = new SolidColorBrush(Color.FromRgb(100, 149, 237)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            Grid.SetRow(confirmButton, 2);

            // Обработка события нажатия кнопки
            string playerName = null;
            confirmButton.Click += (sender, e) =>
            {
                playerName = string.IsNullOrWhiteSpace(inputBox.Text) ? "Игрок" : inputBox.Text.Trim();
                inputWindow.DialogResult = true;
            };

            // Добавляем элементы в Grid
            grid.Children.Add(prompt);
            grid.Children.Add(inputBox);
            grid.Children.Add(confirmButton);

            // Привязываем Grid к окну
            inputWindow.Content = grid;

            // Показываем окно и возвращаем результат
            inputWindow.ShowDialog();
            return playerName;
        }
    }
}
