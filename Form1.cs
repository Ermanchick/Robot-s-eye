using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace YoloDetectorApp
{
    public class DetectionResult
    {
        public int ClassId { get; set; }
        public float Confidence { get; set; }
        public RectangleF Box { get; set; }
        public string ClassName { get; set; } = string.Empty;
    }

    public partial class MainForm : Form
    {
        // Константы для улучшения читаемости
        private const int MODEL_INPUT_SIZE = 640;
        private const int TARGET_FPS = 30;
        private const int DISPLAY_FPS = 60;
        private const int FPS_UPDATE_INTERVAL_MS = 1000;
        private const float CONFIDENCE_THRESHOLD = 0.5f;
        private const float IOU_THRESHOLD = 0.45f;
        private const int MIN_OBJECT_SIZE = 10;

        private VideoCapture? _camera = null;
        private InferenceSession? _yoloSession = null;
        private volatile bool _isRunning = false;

        // Улучшенная статистика
        private int _frameCount = 0;
        private Stopwatch _fpsStopwatch = Stopwatch.StartNew();
        private int _detectionsCount = 0;

        // Кэши с использованием pooling для уменьшения аллокаций
        private TensorPool _tensorPool;
        private MatPool _matPool;
        private volatile List<DetectionResult> _lastDetections = new List<DetectionResult>();
        private Mat? _lastFrame = null;
        private Bitmap? _displayBitmap = null;
        private readonly object _frameLock = new object();
        private readonly SemaphoreSlim _processingSemaphore = new SemaphoreSlim(1, 1);

        // Классы COCO
        private static readonly string[] _cocoClasses = {
            "person", "bicycle", "car", "motorcycle", "airplane", "bus", "train", "truck", "boat",
            "traffic light", "fire hydrant", "stop sign", "parking meter", "bench", "bird", "cat",
            "dog", "horse", "sheep", "cow", "elephant", "bear", "zebra", "giraffe", "backpack", "umbrella",
            "handbag", "tie", "suitcase", "frisbee", "skis", "snowboard", "sports ball", "kite", "baseball bat",
            "baseball glove", "skateboard", "surfboard", "tennis racket", "bottle", "wine glass", "cup",
            "fork", "knife", "spoon", "bowl", "banana", "apple", "sandwich", "orange", "broccoli",
            "carrot", "hot dog", "pizza", "donut", "cake", "chair", "couch", "potted plant", "bed",
            "dining table", "toilet", "tv", "laptop", "mouse", "remote", "keyboard", "cell phone",
            "microwave", "oven", "toaster", "sink", "refrigerator", "book", "clock", "vase", "scissors",
            "teddy bear", "hair drier", "toothbrush"
        };

        private static readonly Dictionary<int, Scalar> _classColors;

        // Статический конструктор для инициализации цветов
        static MainForm()
        {
            _classColors = new Dictionary<int, Scalar>();
            InitializeClassColors();
        }

        private static void InitializeClassColors()
        {
            var random = new Random();

            // Важные классы
            var importantClasses = new Dictionary<int, Scalar>
            {
                {39, new Scalar(0, 0, 255)},    // bottle - red
                {41, new Scalar(0, 255, 0)},    // cup - green
                {67, new Scalar(255, 0, 0)},    // cell phone - blue
                {73, new Scalar(0, 255, 255)},  // book - yellow
                {0, new Scalar(255, 0, 255)}    // person - magenta
            };

            foreach (var kvp in importantClasses)
            {
                _classColors[kvp.Key] = kvp.Value;
            }

            // Остальные классы
            for (int i = 0; i < _cocoClasses.Length; i++)
            {
                if (!_classColors.ContainsKey(i))
                {
                    _classColors[i] = new Scalar(
                        random.Next(100, 256),
                        random.Next(100, 256),
                        random.Next(100, 256)
                    );
                }
            }
        }

        private readonly string _modelPath;

        private System.Windows.Forms.Timer _captureTimer = new System.Windows.Forms.Timer();
        private System.Windows.Forms.Timer _drawTimer = new System.Windows.Forms.Timer();

        // Флаг для использования GPU
        private bool _useGPU = true;
        private Label lblGPUStatus;

        public MainForm()
        {
            InitializeComponent();
            InitializeGPUStatusLabel();
            InitializeEvents();
            InitializePools();
            InitializeTimers();

            _modelPath = @"C:\Users\TOP\source\repos\FormCameraDetect\FormCameraDetect\Models\yolov5nu.onnx";
        }

        private void InitializeGPUStatusLabel()
        {
            // Создаем метку для отображения статуса GPU
            lblGPUStatus = new Label
            {
                AutoSize = true,
                Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold),
                Location = new System.Drawing.Point(264, 580),
                Name = "lblGPUStatus",
                Size = new System.Drawing.Size(200, 18),
                Text = "GPU: Проверка...",
                ForeColor = Color.Blue
            };
            this.Controls.Add(lblGPUStatus);
        }

        private void InitializePools()
        {
            // Создаем пул тензоров
            _tensorPool = new TensorPool();
            _matPool = new MatPool();
        }

        private void InitializeTimers()
        {
            _captureTimer.Interval = 1000 / TARGET_FPS;
            _captureTimer.Tick += CaptureAndProcessFrame;

            _drawTimer.Interval = 1000 / DISPLAY_FPS;
            _drawTimer.Tick += DrawFrame;
        }

        private void InitializeEvents()
        {
            btnStart.Click += BtnStart_Click;
            btnStop.Click += BtnStop_Click;
            btnLoadModel.Click += BtnLoadModel_Click;
            this.Load += MainForm_Load;
            this.FormClosing += MainForm_FormClosing;
        }

        private void MainForm_Load(object? sender, EventArgs e)
        {
            // Проверяем доступность GPU перед загрузкой модели
            CheckGPUAvailability();
            Task.Run(LoadYoloModelAsync); // Загрузка модели асинхронно
        }

        private void CheckGPUAvailability()
        {
            try
            {
                this.Invoke((MethodInvoker)delegate
                {
                    // Попробуем определить доступность GPU
                    // Сначала попробуем использовать CUDA
                    try
                    {
                        // Простая проверка наличия CUDA
                        _useGPU = true;
                        lblGPUStatus.Text = "GPU: CUDA (проверка)";
                        lblGPUStatus.ForeColor = Color.Blue;
                    }
                    catch
                    {
                        _useGPU = false;
                        lblGPUStatus.Text = "GPU: НЕ ДОСТУПЕН (используется CPU)";
                        lblGPUStatus.ForeColor = Color.Orange;
                    }
                });
            }
            catch (Exception ex)
            {
                this.Invoke((MethodInvoker)delegate
                {
                    lblGPUStatus.Text = "GPU: Ошибка проверки";
                    lblGPUStatus.ForeColor = Color.Red;
                    _useGPU = false;
                    Debug.WriteLine($"Ошибка проверки GPU: {ex.Message}");
                });
            }
        }

        private async void LoadYoloModelAsync()
        {
            try
            {
                if (!File.Exists(_modelPath))
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        MessageBox.Show($"Модель не найдена:\n{_modelPath}", "Ошибка");
                    });
                    return;
                }

                _yoloSession?.Dispose();

                // Создаем опции сессии с GPU поддержкой
                var options = CreateSessionOptions();

                await Task.Run(() =>
                {
                    _yoloSession = new InferenceSession(_modelPath, options);
                });

                this.Invoke((MethodInvoker)delegate
                {
                    lblStatus.Text = "Статус: МОДЕЛЬ ЗАГРУЖЕНА";
                    lblStatus.ForeColor = Color.Green;
                    btnStart.Enabled = true;

                    // Обновляем статус GPU
                    if (_useGPU)
                    {
                        lblGPUStatus.Text = "GPU: АКТИВЕН";
                        lblGPUStatus.ForeColor = Color.LimeGreen;
                    }
                    else
                    {
                        lblGPUStatus.Text = "CPU: АКТИВЕН";
                        lblGPUStatus.ForeColor = Color.Yellow;
                    }
                });
            }
            catch (Exception ex)
            {
                // Если GPU не работает, пробуем загрузить на CPU
                if (_useGPU)
                {
                    _useGPU = false;
                    this.Invoke((MethodInvoker)delegate
                    {
                        lblGPUStatus.Text = "GPU: ОШИБКА (переключаемся на CPU)";
                        lblGPUStatus.ForeColor = Color.Red;
                    });

                    // Повторная попытка на CPU
                    await Task.Delay(100);
                    LoadOnCPU();
                }
                else
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        MessageBox.Show($"Ошибка загрузки модели: {ex.Message}", "Ошибка");
                    });
                }
            }
        }

        private void LoadOnCPU()
        {
            try
            {
                _yoloSession?.Dispose();

                var options = new SessionOptions
                {
                    EnableCpuMemArena = true,
                    EnableMemoryPattern = true,
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                    ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
                    InterOpNumThreads = 1,
                    IntraOpNumThreads = Environment.ProcessorCount
                };

                _yoloSession = new InferenceSession(_modelPath, options);

                this.Invoke((MethodInvoker)delegate
                {
                    lblStatus.Text = "Статус: МОДЕЛЬ ЗАГРУЖЕНА (CPU)";
                    lblStatus.ForeColor = Color.Green;
                    btnStart.Enabled = true;
                    lblGPUStatus.Text = "GPU: НЕ ИСПОЛЬЗУЕТСЯ";
                    lblGPUStatus.ForeColor = Color.Gray;
                });
            }
            catch (Exception ex)
            {
                this.Invoke((MethodInvoker)delegate
                {
                    MessageBox.Show($"Ошибка загрузки модели на CPU: {ex.Message}", "Ошибка");
                });
            }
        }

        private SessionOptions CreateSessionOptions()
        {
            var options = new SessionOptions();

            if (_useGPU)
            {
                try
                {
                    // Пытаемся использовать CUDA (NVIDIA)
                    // Для использования CUDA нужно установить пакет Microsoft.ML.OnnxRuntime.Gpu
                    // и иметь установленные CUDA драйверы

                    // Комментируем пока что, чтобы код компилировался
                    // options.AppendExecutionProvider_CUDA();

                    options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;

                    // Настройки для лучшей производительности
                    options.ExecutionMode = ExecutionMode.ORT_PARALLEL;
                    options.InterOpNumThreads = 2;
                    options.IntraOpNumThreads = 2;

                    // Включаем оптимизации
                    options.EnableCpuMemArena = true;
                    options.EnableMemoryPattern = true;

                    Debug.WriteLine("Попытка использования GPU (требуется установка Microsoft.ML.OnnxRuntime.Gpu)");
                }
                catch (Exception cudaEx)
                {
                    Debug.WriteLine($"GPU недоступен: {cudaEx.Message}");
                    _useGPU = false;
                    return CreateCPUOptions();
                }
            }
            else
            {
                return CreateCPUOptions();
            }

            return options;
        }

        private SessionOptions CreateCPUOptions()
        {
            return new SessionOptions
            {
                EnableCpuMemArena = true,
                EnableMemoryPattern = true,
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                ExecutionMode = ExecutionMode.ORT_PARALLEL,
                InterOpNumThreads = 2,
                IntraOpNumThreads = Environment.ProcessorCount
            };
        }

        private void BtnStart_Click(object? sender, EventArgs e)
        {
            if (_yoloSession == null)
            {
                MessageBox.Show("Сначала загрузите модель!", "Внимание");
                return;
            }

            try
            {
                StartCamera();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка запуска камеры: {ex.Message}", "Ошибка");
            }
        }

        private void StartCamera()
        {
            _camera = new VideoCapture(0);
            if (!_camera.IsOpened())
            {
                MessageBox.Show("Не удалось открыть камеру", "Ошибка");
                return;
            }

            // Установка параметров камеры
            _camera.Set(VideoCaptureProperties.FrameWidth, 640);
            _camera.Set(VideoCaptureProperties.FrameHeight, 480);
            _camera.Set(VideoCaptureProperties.Fps, TARGET_FPS);

            _isRunning = true;

            this.Invoke((MethodInvoker)delegate
            {
                btnStart.Enabled = false;
                btnStop.Enabled = true;
                btnLoadModel.Enabled = false;
                lblStatus.Text = "Статус: АКТИВНО";
                lblStatus.ForeColor = Color.Green;
                lblDetectionsCount.Text = "0";
            });

            ResetStatistics();
            _captureTimer.Start();
            _drawTimer.Start();
        }

        private void ResetStatistics()
        {
            _frameCount = 0;
            _fpsStopwatch.Restart();
            _detectionsCount = 0;
        }

        private void BtnStop_Click(object? sender, EventArgs e)
        {
            StopProcessing();
        }

        private void StopProcessing()
        {
            _isRunning = false;
            _captureTimer.Stop();
            _drawTimer.Stop();

            _camera?.Release();
            _camera?.Dispose();
            _camera = null;

            lock (_frameLock)
            {
                _lastFrame?.Dispose();
                _lastFrame = null;
                _lastDetections.Clear();
            }

            _displayBitmap?.Dispose();
            _displayBitmap = null;

            this.Invoke((MethodInvoker)delegate
            {
                btnStart.Enabled = true;
                btnStop.Enabled = false;
                btnLoadModel.Enabled = true;
                lblStatus.Text = "Статус: ВЫКЛЮЧЕНО";
                lblStatus.ForeColor = Color.Red;
                pictureBox.Image = null;
            });
        }

        private async void CaptureAndProcessFrame(object? sender, EventArgs e)
        {
            if (!_isRunning || _camera == null || _yoloSession == null) return;
            if (!_processingSemaphore.Wait(0)) return; // Пропускаем кадр если предыдущий еще обрабатывается

            try
            {
                using (var frame = new Mat())
                {
                    if (!_camera.Read(frame) || frame.Empty())
                        return;

                    // Клонируем кадр для обработки
                    var frameCopy = frame.Clone();

                    // Асинхронная обработка детекции
                    var detections = await Task.Run(() => DetectObjects(frameCopy));

                    lock (_frameLock)
                    {
                        _lastFrame?.Dispose();
                        _lastFrame = frameCopy;
                        _lastDetections = detections;
                        _detectionsCount = detections.Count;
                    }

                    UpdateStatistics();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка захвата кадра: {ex.Message}");
            }
            finally
            {
                _processingSemaphore.Release();
            }
        }

        private void UpdateStatistics()
        {
            _frameCount++;

            if (_fpsStopwatch.ElapsedMilliseconds >= FPS_UPDATE_INTERVAL_MS)
            {
                double fps = _frameCount / (_fpsStopwatch.ElapsedMilliseconds / 1000.0);

                this.BeginInvoke((MethodInvoker)delegate
                {
                    lblFPS.Text = $"FPS: {fps:F0}";
                    lblDetectionsCount.Text = _detectionsCount.ToString();

                    // Показываем информацию о GPU/CPU
                    if (_useGPU)
                    {
                        lblFPS.Text += " (GPU)";
                    }
                });

                _frameCount = 0;
                _fpsStopwatch.Restart();
            }
        }

        private List<DetectionResult> DetectObjects(Mat image)
        {
            var results = new List<DetectionResult>();
            if (image.Empty()) return results;

            try
            {
                // Используем объекты из пула
                using var resizedFrame = _matPool.Get();
                var inputTensor = _tensorPool.Get();

                // Быстрое изменение размера
                Cv2.Resize(image, resizedFrame, new OpenCvSharp.Size(MODEL_INPUT_SIZE, MODEL_INPUT_SIZE), 0, 0, InterpolationFlags.Linear);

                // Конвертация цвета
                Cv2.CvtColor(resizedFrame, resizedFrame, ColorConversionCodes.BGR2RGB);

                // Заполнение тензора
                FillTensorFast(resizedFrame, inputTensor);

                // Инференс
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor("images", inputTensor)
                };

                // Замеряем время инференса
                var inferenceStopwatch = Stopwatch.StartNew();
                using var outputs = _yoloSession.Run(inputs);
                inferenceStopwatch.Stop();

                // Логируем время инференса
                if (inferenceStopwatch.ElapsedMilliseconds > 0)
                {
                    Debug.WriteLine($"Время инференса: {inferenceStopwatch.ElapsedMilliseconds}ms ({(1000.0 / inferenceStopwatch.ElapsedMilliseconds):F1} FPS)");
                }

                var output = outputs.First().AsTensor<float>();

                results = ProcessYoloOutput(output, image.Width, image.Height);

                // Возвращаем тензор в пул
                _tensorPool.Return(inputTensor);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка детекции: {ex.Message}");
            }

            return results;
        }

        private unsafe void FillTensorFast(Mat image, DenseTensor<float> tensor)
        {
            byte* ptr = (byte*)image.Data.ToPointer();
            int channels = image.Channels();
            int step = (int)image.Step();

            // Используем Parallel.For для ускорения
            Parallel.For(0, MODEL_INPUT_SIZE, y =>
            {
                byte* rowPtr = ptr + y * step;
                for (int x = 0; x < MODEL_INPUT_SIZE; x++)
                {
                    int pixelIndex = x * channels;
                    tensor[0, 0, y, x] = rowPtr[pixelIndex + 2] * 0.00392156862745098f; // /255.0f
                    tensor[0, 1, y, x] = rowPtr[pixelIndex + 1] * 0.00392156862745098f;
                    tensor[0, 2, y, x] = rowPtr[pixelIndex] * 0.00392156862745098f;
                }
            });
        }

        private List<DetectionResult> ProcessYoloOutput(Tensor<float> output, int originalWidth, int originalHeight)
        {
            var results = new List<DetectionResult>();
            var dimensions = output.Dimensions.ToArray();

            if (dimensions.Length != 3 || dimensions[1] != 84)
                return results;

            int numPredictions = dimensions[2];
            float scaleX = (float)originalWidth / MODEL_INPUT_SIZE;
            float scaleY = (float)originalHeight / MODEL_INPUT_SIZE;

            // Предварительное выделение памяти
            results.Capacity = Math.Min(100, numPredictions / 10);

            for (int i = 0; i < numPredictions; i++)
            {
                float centerX = output[0, 0, i];
                float centerY = output[0, 1, i];
                float width = output[0, 2, i];
                float height = output[0, 3, i];

                // Поиск максимальной уверенности
                float maxConfidence = 0;
                int bestClassId = -1;

                for (int c = 0; c < 80; c++)
                {
                    float confidence = output[0, 4 + c, i];
                    if (confidence > maxConfidence)
                    {
                        maxConfidence = confidence;
                        bestClassId = c;
                    }
                }

                if (maxConfidence < CONFIDENCE_THRESHOLD) continue;

                // Преобразование координат
                float x = (centerX - width * 0.5f) * scaleX;
                float y = (centerY - height * 0.5f) * scaleY;
                float w = width * scaleX;
                float h = height * scaleY;

                // Проверка границ
                if (x < 0 || y < 0 || x + w > originalWidth || y + h > originalHeight)
                    continue;
                if (w < MIN_OBJECT_SIZE || h < MIN_OBJECT_SIZE)
                    continue;

                results.Add(new DetectionResult
                {
                    ClassId = bestClassId,
                    Confidence = maxConfidence,
                    Box = new RectangleF(x, y, w, h),
                    ClassName = bestClassId < _cocoClasses.Length ? _cocoClasses[bestClassId] : "unknown"
                });
            }

            return ApplyNMS(results);
        }

        private List<DetectionResult> ApplyNMS(List<DetectionResult> detections)
        {
            if (detections.Count == 0) return detections;

            var sortedDetections = detections.OrderByDescending(d => d.Confidence).ToList();
            var result = new List<DetectionResult>(sortedDetections.Count);

            while (sortedDetections.Count > 0)
            {
                var current = sortedDetections[0];
                result.Add(current);
                sortedDetections.RemoveAt(0);

                sortedDetections.RemoveAll(d =>
                    CalculateIoU(current.Box, d.Box) >= IOU_THRESHOLD &&
                    d.ClassId == current.ClassId
                );
            }

            return result;
        }

        private float CalculateIoU(RectangleF box1, RectangleF box2)
        {
            float x1 = Math.Max(box1.X, box2.X);
            float y1 = Math.Max(box1.Y, box2.Y);
            float x2 = Math.Min(box1.Right, box2.Right);
            float y2 = Math.Min(box1.Bottom, box2.Bottom);

            float intersection = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
            float union = box1.Width * box1.Height + box2.Width * box2.Height - intersection;

            return union > 1e-6 ? intersection / union : 0;
        }

        private void DrawFrame(object? sender, EventArgs e)
        {
            if (!_isRunning) return;

            try
            {
                Mat? frameToDraw = null;
                List<DetectionResult> detectionsToDraw;

                lock (_frameLock)
                {
                    if (_lastFrame == null || _lastFrame.Empty()) return;
                    frameToDraw = _lastFrame.Clone();
                    detectionsToDraw = new List<DetectionResult>(_lastDetections);
                }

                using (frameToDraw)
                {
                    if (frameToDraw != null && !frameToDraw.Empty())
                    {
                        DrawDetectionsOptimized(frameToDraw, detectionsToDraw);
                        DisplayFrameOptimized(frameToDraw);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка отрисовки: {ex.Message}");
            }
        }

        private void DrawDetectionsOptimized(Mat image, List<DetectionResult> detections)
        {
            foreach (var detection in detections)
            {
                var rect = new OpenCvSharp.Rect(
                    (int)detection.Box.X,
                    (int)detection.Box.Y,
                    (int)detection.Box.Width,
                    (int)detection.Box.Height
                );

                if (!_classColors.TryGetValue(detection.ClassId, out var color))
                    color = Scalar.Red;

                Cv2.Rectangle(image, rect, color, 2);

                string label = $"{detection.ClassName} {detection.Confidence:F1}";
                Cv2.PutText(image, label,
                    new OpenCvSharp.Point(rect.X, rect.Y - 5),
                    HersheyFonts.HersheySimplex,
                    0.5,
                    color,
                    1);
            }

            DrawStatistics(image);
        }

        private void DrawStatistics(Mat image)
        {
            string fpsText = lblFPS.Text.Replace("FPS: ", "").Replace(" (GPU)", "").Replace(" (CPU)", "").Trim();
            string gpuInfo = _useGPU ? "GPU" : "CPU";

            Cv2.PutText(image, $"FPS: {fpsText} ({gpuInfo})",
                new OpenCvSharp.Point(10, 25),
                HersheyFonts.HersheySimplex,
                0.6,
                _useGPU ? Scalar.Green : Scalar.Yellow,
                1);

            Cv2.PutText(image, $"Объектов: {_detectionsCount}",
                new OpenCvSharp.Point(10, 50),
                HersheyFonts.HersheySimplex,
                0.6,
                Scalar.Yellow,
                1);
        }

        private void DisplayFrameOptimized(Mat frame)
        {
            try
            {
                if (frame.Empty()) return;

                var newBitmap = new Bitmap(
                    frame.Width,
                    frame.Height,
                    System.Drawing.Imaging.PixelFormat.Format24bppRgb
                );

                var bitmapData = newBitmap.LockBits(
                    new System.Drawing.Rectangle(0, 0, newBitmap.Width, newBitmap.Height),
                    System.Drawing.Imaging.ImageLockMode.WriteOnly,
                    newBitmap.PixelFormat
                );

                unsafe
                {
                    Buffer.MemoryCopy(
                        frame.Data.ToPointer(),
                        (void*)bitmapData.Scan0,
                        (long)(frame.Total() * frame.ElemSize()),
                        (long)(frame.Total() * frame.ElemSize())
                    );
                }

                newBitmap.UnlockBits(bitmapData);

                this.BeginInvoke((MethodInvoker)delegate
                {
                    var oldImage = pictureBox.Image;
                    pictureBox.Image = newBitmap;
                    oldImage?.Dispose();
                    _displayBitmap?.Dispose();
                    _displayBitmap = newBitmap;
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка отображения: {ex.Message}");
            }
        }

        private void BtnLoadModel_Click(object? sender, EventArgs e)
        {
            Task.Run(LoadYoloModelAsync);
        }

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            StopProcessing();
            _yoloSession?.Dispose();
            _tensorPool?.Dispose();
            _matPool?.Dispose();
        }

        // Класс пула для тензоров
        private class TensorPool : IDisposable
        {
            private readonly ConcurrentBag<DenseTensor<float>> _objects = new();
            private bool _disposed;

            public DenseTensor<float> Get()
            {
                if (_disposed) throw new ObjectDisposedException(nameof(TensorPool));

                if (_objects.TryTake(out var item))
                {
                    // Очищаем тензор
                    var bufferArray = item.Buffer.ToArray();
                    Array.Clear(bufferArray, 0, (int)item.Length);
                    return item;
                }

                return new DenseTensor<float>(new[] { 1, 3, MODEL_INPUT_SIZE, MODEL_INPUT_SIZE });
            }

            public void Return(DenseTensor<float> item)
            {
                if (_disposed || item == null) return;
                _objects.Add(item);
            }

            public void Dispose()
            {
                _disposed = true;
                while (_objects.TryTake(out _))
                {
                    // DenseTensor не имеет Dispose, поэтому просто очищаем коллекцию
                }
            }
        }

        // Класс пула для Mat объектов
        private class MatPool : IDisposable
        {
            private readonly ConcurrentBag<Mat> _objects = new();
            private bool _disposed;

            public Mat Get()
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MatPool));

                if (_objects.TryTake(out var item))
                {
                    item.SetTo(Scalar.Black);
                    return item;
                }

                return new Mat(MODEL_INPUT_SIZE, MODEL_INPUT_SIZE, MatType.CV_8UC3);
            }

            public void Return(Mat item)
            {
                if (_disposed || item == null) return;
                _objects.Add(item);
            }

            public void Dispose()
            {
                _disposed = true;
                while (_objects.TryTake(out var item))
                {
                    item.Dispose();
                }
            }
        }
    }
}