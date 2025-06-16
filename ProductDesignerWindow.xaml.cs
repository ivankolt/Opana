using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using Npgsql;
using System.IO;

namespace UchPR
{
    public partial class ProductDesignerWindow : Window
    {
        private DataBase database;
        private string currentUserLogin;
        private string currentUserPassword;
        private ObservableCollection<DesignAccessoryItem> designAccessories;
        private UIElement selectedElement;
        private Point lastMousePosition;
        private bool isDragging = false;
        private int? currentDesignId;
        private bool isResizing = false;
        private Point resizeStartPoint;
        private Size originalSize;

        public ProductDesignerWindow(string userLogin, string userPassword)
        {
            InitializeComponent();
            database = new DataBase();
            currentUserLogin = userLogin;
            currentUserPassword = userPassword;
            designAccessories = new ObservableCollection<DesignAccessoryItem>();

            InitializeDesigner();
            LoadMaterials();
        }

        private void InitializeDesigner()
        {
            lbAccessories.ItemsSource = designAccessories;

            // Инициализация цветов окантовки
            cbBorderColor.Items.Add("Черный");
            cbBorderColor.Items.Add("Белый");
            cbBorderColor.Items.Add("Коричневый");
            cbBorderColor.Items.Add("Серый");
            cbBorderColor.SelectedIndex = 0;

            // Инициализация полей размера
            tbAccessoryWidth.Text = "50";
            tbAccessoryHeight.Text = "50";

            UpdateProductDimensions();
        }

        private void LoadMaterials()
        {
            try
            {
                LoadFabrics();
                LoadBorders();
                LoadAccessories();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки материалов: {ex.Message}");
            }
        }

        private void LoadFabrics()
        {
            try
            {
                // Загрузка стандартных тканей
                string query = @"
                    SELECT f.article, fn.name, f.image 
                    FROM fabric f
                    LEFT JOIN fabricname fn ON f.name_id = fn.code
                    ORDER BY fn.name";

                var fabricData = database.GetData(query);
                var fabrics = new List<MaterialItem>();

                foreach (DataRow row in fabricData.Rows)
                {
                    fabrics.Add(new MaterialItem
                    {
                        Id = row["article"].ToString(),
                        Name = row["name"].ToString(),
                        ImagePath = GetImagePath("Ткани", row["article"].ToString()),
                        Type = "fabric"
                    });
                }

                // Загрузка пользовательских тканей
                string customQuery = @"
                    SELECT id, name, image_path 
                    FROM custom_materials 
                    WHERE user_login = @login AND user_password = @password 
                    AND material_type = 'fabric' AND is_active = true
                    ORDER BY name";

                var parameters = new NpgsqlParameter[]
                {
                    new NpgsqlParameter("@login", currentUserLogin),
                    new NpgsqlParameter("@password", currentUserPassword)
                };

                var customData = database.GetData(customQuery, parameters);

                foreach (DataRow row in customData.Rows)
                {
                    fabrics.Add(new MaterialItem
                    {
                        Id = "custom_" + row["id"].ToString(),
                        Name = row["name"].ToString() + " (пользовательская)",
                        ImagePath = row["image_path"].ToString(),
                        Type = "custom_fabric"
                    });
                }

                cbFabric.ItemsSource = fabrics;
                cbFabric.DisplayMemberPath = "Name";

                if (fabrics.Count > 0)
                    cbFabric.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки тканей: {ex.Message}");
            }
        }
        private void TbAccessorySize_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Обновляем размер выбранной фурнитуры при изменении значений в текстовых полях
            if (selectedElement != null && selectedElement is Image image)
            {
                if (double.TryParse(tbAccessoryWidth.Text, out double width) && width > 0 &&
                    double.TryParse(tbAccessoryHeight.Text, out double height) && height > 0)
                {
                    width = Math.Max(20, Math.Min(200, width));
                    height = Math.Max(20, Math.Min(200, height));

                    image.Width = width;
                    image.Height = height;

                    UpdateAccessorySize(width, height);
                }
            }
        }

        private void BtnResetSize_Click(object sender, RoutedEventArgs e)
        {
            if (selectedElement != null && selectedElement is Image image)
            {
                image.Width = 50;
                image.Height = 50;

                tbAccessoryWidth.Text = "50";
                tbAccessoryHeight.Text = "50";

                UpdateAccessorySize(50, 50);
                Console.WriteLine("Размер сброшен к 50x50");
            }
        }

        private void BtnApplySize_Click(object sender, RoutedEventArgs e)
        {
            if (selectedElement != null && selectedElement is Image image)
            {
                if (double.TryParse(tbAccessoryWidth.Text, out double width) && width > 0 &&
                    double.TryParse(tbAccessoryHeight.Text, out double height) && height > 0)
                {
                    width = Math.Max(20, Math.Min(200, width));
                    height = Math.Max(20, Math.Min(200, height));

                    image.Width = width;
                    image.Height = height;

                    UpdateAccessorySize(width, height);
                    Console.WriteLine($"Применен размер: {width}x{height}");
                }
            }
        }


        private void LoadBorders()
        {
            var borders = new List<string> { "Стандартная", "Декоративная", "Усиленная", "Без окантовки" };
            cbBorder.ItemsSource = borders;
            cbBorder.SelectedIndex = 0;
        }

        private void LoadAccessories()
        {
            try
            {
                // Загрузка стандартной фурнитуры
                string query = @"
                    SELECT a.article, fan.name, a.image 
                    FROM accessory a
                    LEFT JOIN furnitureaccessoryname fan ON a.name_id = fan.id
                    ORDER BY fan.name";

                var accessoryData = database.GetData(query);
                var accessories = new List<MaterialItem>();

                foreach (DataRow row in accessoryData.Rows)
                {
                    accessories.Add(new MaterialItem
                    {
                        Id = row["article"].ToString(),
                        Name = row["name"].ToString(),
                        ImagePath = GetImagePath("Фурнитура", row["article"].ToString()),
                        Type = "accessory"
                    });
                }

                // Загрузка пользовательской фурнитуры
                string customQuery = @"
                    SELECT id, name, image_path 
                    FROM custom_materials 
                    WHERE user_login = @login AND user_password = @password 
                    AND material_type = 'accessory' AND is_active = true
                    ORDER BY name";

                var parameters = new NpgsqlParameter[]
                {
                    new NpgsqlParameter("@login", currentUserLogin),
                    new NpgsqlParameter("@password", currentUserPassword)
                };

                var customData = database.GetData(customQuery, parameters);

                foreach (DataRow row in customData.Rows)
                {
                    accessories.Add(new MaterialItem
                    {
                        Id = "custom_" + row["id"].ToString(),
                        Name = row["name"].ToString() + " (пользовательская)",
                        ImagePath = row["image_path"].ToString(),
                        Type = "custom_accessory"
                    });
                }

                cbAccessory.ItemsSource = accessories;
                cbAccessory.DisplayMemberPath = "Name";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки фурнитуры: {ex.Message}");
            }
        }

        private string GetImagePath(string folder, string article)
        {
            Console.WriteLine($"=== GetImagePath Debug ===");
            Console.WriteLine($"Folder: {folder}");
            Console.WriteLine($"Article: {article}");

            string[] extensions = { ".jpg", ".jpeg", ".png", ".bmp" };

            // ИСПРАВЛЕНО: Полностью убираем bin из пути
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            Console.WriteLine($"Original base directory: {baseDirectory}");

            // Поднимаемся вверх до корня проекта (убираем bin\Debug или bin\Release)
            string projectRoot = baseDirectory;

            // Убираем bin\Debug или bin\Release
            if (projectRoot.Contains("\\bin\\"))
            {
                int binIndex = projectRoot.IndexOf("\\bin\\");
                projectRoot = projectRoot.Substring(0, binIndex);
            }

            Console.WriteLine($"Project root (without bin): {projectRoot}");

            foreach (string ext in extensions)
            {
                // Формируем путь напрямую к папке Images в корне проекта
                string fullPath = System.IO.Path.Combine(projectRoot, "Images", folder, $"{article}{ext}");
                Console.WriteLine($"Checking: {fullPath}");
                Console.WriteLine($"File exists: {System.IO.File.Exists(fullPath)}");

                if (System.IO.File.Exists(fullPath))
                {
                    Console.WriteLine($"✓ Found image! Full path: {fullPath}");
                    return fullPath;
                }
            }

            // Проверяем default изображение
            string defaultFullPath = System.IO.Path.Combine(projectRoot, "Images", folder, "default.jpg");
            Console.WriteLine($"Checking default path: {defaultFullPath}");
            Console.WriteLine($"Default exists: {System.IO.File.Exists(defaultFullPath)}");

            if (System.IO.File.Exists(defaultFullPath))
            {
                Console.WriteLine($"✓ Using default image! Full path: {defaultFullPath}");
                return defaultFullPath;
            }

            Console.WriteLine($"❌ No image found for {folder}/{article}");
            return string.Empty;
        }


        // ======================= ОБРАБОТЧИКИ СОБЫТИЙ ИНТЕРФЕЙСА =======================

        private void TbDimensions_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateProductDimensions();
        }

        private void UpdateProductDimensions()
        {
            if (MainProduct == null) return;

            if (double.TryParse(tbWidth.Text, out double width) && width > 0)
            {
                MainProduct.Width = Math.Max(50, Math.Min(800, width));
            }

            if (double.TryParse(tbHeight.Text, out double height) && height > 0)
            {
                MainProduct.Height = Math.Max(50, Math.Min(600, height));
            }
        }

        private void CbFabric_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedFabric = cbFabric.SelectedItem as MaterialItem;
            if (selectedFabric != null)
            {
                try
                {
                    Console.WriteLine($"Loading fabric image: {selectedFabric.ImagePath}");

                    var imageBrush = new ImageBrush();
                    var bitmapImage = CreateBitmapImageSafely(selectedFabric.ImagePath);

                    if (bitmapImage != null)
                    {
                        imageBrush.ImageSource = bitmapImage;
                        imageBrush.Stretch = Stretch.UniformToFill;
                        MainProduct.Fill = imageBrush;
                        imgFabricPreview.Source = bitmapImage;
                        Console.WriteLine("✓ Image loaded successfully!");
                    }
                    else
                    {
                        MainProduct.Fill = Brushes.LightGray;
                        Console.WriteLine("❌ Failed to load image, using default color");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error loading image: {ex.Message}");
                    MainProduct.Fill = Brushes.White;
                }
            }
        }

        private BitmapImage CreateBitmapImageSafely(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath))
            {
                Console.WriteLine("Image path is empty");
                return null;
            }

            try
            {
                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();

                // ИСПРАВЛЕНИЕ: Добавляем дополнительные параметры для избежания ошибок
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.CreateOptions = BitmapCreateOptions.IgnoreImageCache;

                // Проверяем тип URI
                if (imagePath.StartsWith("pack://"))
                {
                    bitmapImage.UriSource = new Uri(imagePath, UriKind.Absolute);
                }
                else if (System.IO.File.Exists(imagePath))
                {
                    bitmapImage.UriSource = new Uri(imagePath, UriKind.Absolute);
                }
                else
                {
                    Console.WriteLine($"File not found: {imagePath}");
                    bitmapImage.EndInit();
                    return null;
                }

                bitmapImage.EndInit();
                bitmapImage.Freeze(); // Важно для многопоточности

                return bitmapImage;
            }
            catch (NotSupportedException ex)
            {
                Console.WriteLine($"NotSupportedException: {ex.Message}");
                return CreateFallbackImage();
            }
            catch (System.IO.FileNotFoundException ex)
            {
                Console.WriteLine($"File not found: {ex.Message}");
                return CreateFallbackImage();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"General exception: {ex.Message}");
                return CreateFallbackImage();
            }
        }

        private BitmapImage CreateFallbackImage()
        {
            try
            {
                // Создаем простое изображение-заглушку
                var fallbackBitmap = new BitmapImage();
                fallbackBitmap.BeginInit();
                fallbackBitmap.UriSource = new Uri("pack://application:,,,/Images/default.png", UriKind.Absolute);
                fallbackBitmap.CacheOption = BitmapCacheOption.OnLoad;
                fallbackBitmap.EndInit();
                fallbackBitmap.Freeze();
                return fallbackBitmap;
            }
            catch
            {
                return null;
            }
        }

        private void CbBorder_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateBorder();
        }

        private void CbBorderColor_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateBorder();
        }

        private void TbBorderWidth_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateBorder();
        }

        private void UpdateBorder()
        {
            if (MainProduct == null) return;

            var borderType = cbBorder.SelectedItem?.ToString();
            var borderColor = cbBorderColor.SelectedItem?.ToString();

            if (borderType == "Без окантовки")
            {
                MainProduct.StrokeThickness = 0;
                return;
            }

            if (double.TryParse(tbBorderWidth.Text, out double borderWidth))
            {
                MainProduct.StrokeThickness = Math.Max(1, Math.Min(20, borderWidth));
            }

            // ИСПРАВЛЕНО: Заменен switch expression на обычный switch
            switch (borderColor)
            {
                case "Черный":
                    MainProduct.Stroke = Brushes.Black;
                    break;
                case "Белый":
                    MainProduct.Stroke = Brushes.White;
                    break;
                case "Коричневый":
                    MainProduct.Stroke = Brushes.Brown;
                    break;
                case "Серый":
                    MainProduct.Stroke = Brushes.Gray;
                    break;
                default:
                    MainProduct.Stroke = Brushes.Black;
                    break;
            }
        }

        private void BtnAddAccessoryToDesign_Click(object sender, RoutedEventArgs e)
        {
            var selectedAccessory = cbAccessory.SelectedItem as MaterialItem;
            if (selectedAccessory == null)
            {
                MessageBox.Show("Выберите фурнитуру для добавления");
                return;
            }

            AddAccessoryToCanvas(selectedAccessory);
        }

        private void AddAccessoryToCanvas(MaterialItem accessory)
        {
            try
            {
                var image = new Image
                {
                    Source = new BitmapImage(new Uri(accessory.ImagePath, UriKind.RelativeOrAbsolute)),
                    Width = 50,
                    Height = 50,
                    Cursor = Cursors.SizeAll,
                    Tag = accessory
                };

                // Позиционируем в центре изделия
                double left = Canvas.GetLeft(MainProduct) + MainProduct.Width / 2 - 25;
                double top = Canvas.GetTop(MainProduct) + MainProduct.Height / 2 - 25;

                Canvas.SetLeft(image, left);
                Canvas.SetTop(image, top);

                DesignCanvas.Children.Add(image);

                // Добавляем в список
                var designItem = new DesignAccessoryItem
                {
                    Id = Guid.NewGuid().ToString(),
                    AccessoryArticle = accessory.Id,
                    Name = accessory.Name,
                    ImagePath = accessory.ImagePath,
                    X = left,
                    Y = top,
                    Width = 50,
                    Height = 50,
                    Rotation = 0,
                    CanvasElement = image
                };

                designAccessories.Add(designItem);

                // Обновляем позицию в списке
                designItem.Position = $"X: {left:F0}, Y: {top:F0}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка добавления фурнитуры: {ex.Message}");
            }
        }

        // ======================= DRAG & DROP И ВЗАИМОДЕЙСТВИЕ С CANVAS =======================

        private void DesignCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var element = e.Source as UIElement;
            if (element != null && element != DesignCanvas && element is Image image)
            {
                selectedElement = element;
                lastMousePosition = e.GetPosition(DesignCanvas);

                // Проверяем, нажата ли клавиша Ctrl для изменения размера
                if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                {
                    isResizing = true;
                    resizeStartPoint = e.GetPosition(DesignCanvas);
                    originalSize = new Size(image.Width, image.Height);
                    Console.WriteLine("Режим изменения размера активирован");
                }
                else
                {
                    isDragging = true;
                }

                DesignCanvas.CaptureMouse();
                HighlightSelectedElement();
            }
        }
        private void UpdateAccessoryProperties()
        {
            if (selectedElement == null) return;

            var designItem = designAccessories.FirstOrDefault(x => x.CanvasElement == selectedElement);
            if (designItem != null && selectedElement is Image image)
            {
                designItem.X = Canvas.GetLeft(selectedElement);
                designItem.Y = Canvas.GetTop(selectedElement);
                designItem.Width = image.Width;
                designItem.Height = image.Height;
                designItem.Position = $"X: {designItem.X:F0}, Y: {designItem.Y:F0}, Размер: {designItem.Width:F0}x{designItem.Height:F0}";
            }
        }


        private void DesignCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (isDragging || isResizing)
            {
                isDragging = false;
                isResizing = false;
                DesignCanvas.ReleaseMouseCapture();

                // Обновляем позицию и размер в списке
                UpdateAccessoryProperties();
            }
        }
        private void DesignCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (selectedElement != null && selectedElement is Image image)
            {
                Point currentPosition = e.GetPosition(DesignCanvas);

                if (isResizing)
                {
                    // Изменение размера
                    double deltaX = currentPosition.X - resizeStartPoint.X;
                    double deltaY = currentPosition.Y - resizeStartPoint.Y;

                    // Вычисляем новый размер на основе движения мыши
                    double scaleFactor = 1.0 + (deltaX + deltaY) / 200.0; // Коэффициент масштабирования
                    scaleFactor = Math.Max(0.2, Math.Min(5.0, scaleFactor)); // Ограничиваем размер

                    double newWidth = originalSize.Width * scaleFactor;
                    double newHeight = originalSize.Height * scaleFactor;

                    // Ограничиваем минимальный и максимальный размер
                    newWidth = Math.Max(20, Math.Min(200, newWidth));
                    newHeight = Math.Max(20, Math.Min(200, newHeight));

                    image.Width = newWidth;
                    image.Height = newHeight;

                    Console.WriteLine($"Изменение размера: {newWidth:F0}x{newHeight:F0}");
                }
                else if (isDragging)
                {
                    // Перетаскивание
                    double offsetX = currentPosition.X - lastMousePosition.X;
                    double offsetY = currentPosition.Y - lastMousePosition.Y;

                    double left = Canvas.GetLeft(selectedElement) + offsetX;
                    double top = Canvas.GetTop(selectedElement) + offsetY;

                    // Ограничиваем перемещение границами Canvas
                    left = Math.Max(0, Math.Min(DesignCanvas.Width - selectedElement.RenderSize.Width, left));
                    top = Math.Max(0, Math.Min(DesignCanvas.Height - selectedElement.RenderSize.Height, top));

                    Canvas.SetLeft(selectedElement, left);
                    Canvas.SetTop(selectedElement, top);

                    lastMousePosition = currentPosition;
                }
            }
        }
        private void UpdateAccessorySize(double width, double height)
        {
            if (selectedElement == null) return;

            var designItem = designAccessories.FirstOrDefault(x => x.CanvasElement == selectedElement);
            if (designItem != null)
            {
                designItem.Width = width;
                designItem.Height = height;
                designItem.Position = $"X: {designItem.X:F0}, Y: {designItem.Y:F0}, Размер: {width:F0}x{height:F0}";
            }
        }
        private void DesignCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (selectedElement != null && selectedElement is Image image)
            {
                // Если зажата клавиша Shift - изменяем размер
                if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                {
                    double scaleFactor = e.Delta > 0 ? 1.1 : 0.9;

                    double newWidth = image.Width * scaleFactor;
                    double newHeight = image.Height * scaleFactor;

                    // Ограничиваем размер
                    newWidth = Math.Max(20, Math.Min(200, newWidth));
                    newHeight = Math.Max(20, Math.Min(200, newHeight));

                    image.Width = newWidth;
                    image.Height = newHeight;

                    UpdateAccessorySize(newWidth, newHeight);
                    Console.WriteLine($"Изменение размера колесиком: {newWidth:F0}x{newHeight:F0}");
                }
                else
                {
                    // Поворот (как было раньше)
                    var transform = image.RenderTransform as RotateTransform ?? new RotateTransform();
                    transform.Angle += e.Delta > 0 ? 15 : -15;
                    transform.CenterX = image.Width / 2;
                    transform.CenterY = image.Height / 2;
                    image.RenderTransform = transform;

                    UpdateAccessoryRotation(transform.Angle);
                }
            }
        }

        private void HighlightSelectedElement()
        {
            // Убираем выделение со всех элементов
            foreach (UIElement child in DesignCanvas.Children)
            {
                if (child is Image img)
                {
                    img.Opacity = 1.0;
                }
            }

            // Выделяем текущий элемент
            if (selectedElement is Image selectedImg)
            {
                selectedImg.Opacity = 0.8;
            }
        }

        private void UpdateAccessoryPosition()
        {
            if (selectedElement == null) return;

            var designItem = designAccessories.FirstOrDefault(x => x.CanvasElement == selectedElement);
            if (designItem != null && selectedElement is Image image)
            {
                designItem.X = Canvas.GetLeft(selectedElement);
                designItem.Y = Canvas.GetTop(selectedElement);
                designItem.Position = $"X: {designItem.X:F0}, Y: {designItem.Y:F0}, Размер: {image.Width:F0}x{image.Height:F0}";
            }
        }

        private void UpdateAccessoryRotation(double angle)
        {
            if (selectedElement == null) return;

            var designItem = designAccessories.FirstOrDefault(x => x.CanvasElement == selectedElement);
            if (designItem != null)
            {
                designItem.Rotation = angle;
            }
        }

        private void LbAccessories_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = lbAccessories.SelectedItem as DesignAccessoryItem;
            if (selectedItem != null)
            {
                selectedElement = selectedItem.CanvasElement;
                HighlightSelectedElement();

                // Обновляем поля размера для выбранной фурнитуры
                if (selectedElement is Image image)
                {
                    tbAccessoryWidth.Text = image.Width.ToString("F0");
                    tbAccessoryHeight.Text = image.Height.ToString("F0");
                    Console.WriteLine($"Выбрана фурнитура: {selectedItem.Name}, размер: {image.Width}x{image.Height}");
                }
            }
            else
            {
                // Если ничего не выбрано, устанавливаем значения по умолчанию
                tbAccessoryWidth.Text = "50";
                tbAccessoryHeight.Text = "50";
            }
        }


        private void BtnRemoveAccessory_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var accessoryItem = button?.Tag as DesignAccessoryItem;

            if (accessoryItem != null)
            {
                DesignCanvas.Children.Remove(accessoryItem.CanvasElement);
                designAccessories.Remove(accessoryItem);
            }
        }

        // ======================= ДОБАВЛЕНИЕ ПОЛЬЗОВАТЕЛЬСКИХ МАТЕРИАЛОВ =======================

        private void BtnAddCustomFabric_Click(object sender, RoutedEventArgs e)
        {
            AddCustomMaterial("fabric", "ткань");
        }

        private void BtnAddCustomBorder_Click(object sender, RoutedEventArgs e)
        {
            AddCustomMaterial("border", "окантовку");
        }

        private void BtnAddCustomAccessory_Click(object sender, RoutedEventArgs e)
        {
            AddCustomMaterial("accessory", "фурнитуру");
        }

        private void AddCustomMaterial(string materialType, string materialTypeName)
        {
            var dialog = new OpenFileDialog
            {
                Title = $"Выберите изображение для {materialTypeName}",
                Filter = "Изображения|*.jpg;*.jpeg;*.png;*.bmp;*.gif",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    string fileName = System.IO.Path.GetFileName(dialog.FileName);
                    string destFolder = System.IO.Path.Combine("Images", "Custom", materialType);
                    System.IO.Directory.CreateDirectory(destFolder);

                    string destPath = System.IO.Path.Combine(destFolder, $"{Guid.NewGuid()}_{fileName}");
                    System.IO.File.Copy(dialog.FileName, destPath, true);

                    // ИСПРАВЛЕНО: Заменен Microsoft.VisualBasic.Interaction на простой InputBox
                    string materialName = ShowInputDialog($"Введите название для {materialTypeName}:",
                        "Название материала",
                        System.IO.Path.GetFileNameWithoutExtension(fileName));

                    if (string.IsNullOrWhiteSpace(materialName))
                        return;

                    // Сохранение в БД
                    SaveCustomMaterial(materialType, materialName, destPath, fileName, new FileInfo(dialog.FileName).Length);

                    // Обновление списков
                    LoadMaterials();

                    MessageBox.Show($"Пользовательская {materialTypeName} успешно добавлена!");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка добавления {materialTypeName}: {ex.Message}");
                }
            }
        }

        // ИСПРАВЛЕНО: Простая реализация InputBox
        private string ShowInputDialog(string text, string caption, string defaultValue = "")
        {
            var inputWindow = new Window()
            {
                Width = 400,
                Height = 200,
                Title = caption,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,

                Owner = this
            };

            var stackPanel = new StackPanel { Margin = new Thickness(20) };
            var textBlock = new TextBlock { Text = text, Margin = new Thickness(0, 0, 0, 10) };
            var textBox = new TextBox { Text = defaultValue, Margin = new Thickness(0, 0, 0, 10) };
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var okButton = new Button { Content = "OK", Width = 75, Margin = new Thickness(0, 0, 10, 0) };
            var cancelButton = new Button { Content = "Отмена", Width = 75 };

            bool dialogResult = false;
            okButton.Click += (sender, e) => { dialogResult = true; inputWindow.Close(); };
            cancelButton.Click += (sender, e) => { dialogResult = false; inputWindow.Close(); };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            stackPanel.Children.Add(textBlock);
            stackPanel.Children.Add(textBox);
            stackPanel.Children.Add(buttonPanel);
            inputWindow.Content = stackPanel;

            inputWindow.ShowDialog();
            return dialogResult ? textBox.Text : string.Empty;
        }

        private void SaveCustomMaterial(string materialType, string name, string imagePath, string originalName, long fileSize)
        {
            try
            {
                string query = @"
                    INSERT INTO custom_materials 
                    (user_login, user_password, material_type, name, image_path, image_original_name, file_size, mime_type)
                    VALUES (@login, @password, @materialType, @name, @imagePath, @originalName, @fileSize, @mimeType)";

                var parameters = new NpgsqlParameter[]
                {
                    new NpgsqlParameter("@login", currentUserLogin),
                    new NpgsqlParameter("@password", currentUserPassword),
                    new NpgsqlParameter("@materialType", materialType),
                    new NpgsqlParameter("@name", name),
                    new NpgsqlParameter("@imagePath", imagePath),
                    new NpgsqlParameter("@originalName", originalName),
                    new NpgsqlParameter("@fileSize", fileSize),
                    new NpgsqlParameter("@mimeType", GetMimeType(imagePath))
                };

                // ИСПРАВЛЕНО: Используем существующий метод из DataBase
                using (var connection = new NpgsqlConnection(database.connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand(query, connection))
                    {
                        command.Parameters.AddRange(parameters);
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка сохранения материала в БД: {ex.Message}");
            }
        }

        private string GetMimeType(string filePath)
        {
            string extension = System.IO.Path.GetExtension(filePath).ToLower();

            // ИСПРАВЛЕНО: Заменен switch expression на обычный switch
            switch (extension)
            {
                case ".jpg":
                case ".jpeg":
                    return "image/jpeg";
                case ".png":
                    return "image/png";
                case ".bmp":
                    return "image/bmp";
                case ".gif":
                    return "image/gif";
                default:
                    return "image/jpeg";
            }
        }

        // ======================= СОХРАНЕНИЕ ДИЗАЙНА =======================

        private void BtnSaveDesign_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txtDesignName.Text))
                {
                    MessageBox.Show("Введите название дизайна");
                    return;
                }

                SaveDesign();
                MessageBox.Show("Дизайн успешно сохранен!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения дизайна: {ex.Message}");
            }
        }

        private void SaveDesign()
        {
            using (var connection = new NpgsqlConnection(database.connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Сохранение основного дизайна
                        int designId = SaveProductDesign(connection, transaction);

                        // Сохранение фурнитуры
                        SaveDesignAccessories(connection, transaction, designId);

                        transaction.Commit();
                        currentDesignId = designId;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private int SaveProductDesign(NpgsqlConnection connection, NpgsqlTransaction transaction)
        {
            var selectedFabric = cbFabric.SelectedItem as MaterialItem;

            string query = @"
                INSERT INTO product_designs 
                (user_login, user_password, design_name, width, height, fabric_article, 
                 border_type, border_color, border_width, status)
                VALUES (@login, @password, @designName, @width, @height, @fabricArticle, 
                        @borderType, @borderColor, @borderWidth, 'saved')
                RETURNING id";

            using (var cmd = new NpgsqlCommand(query, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@login", currentUserLogin);
                cmd.Parameters.AddWithValue("@password", currentUserPassword);
                cmd.Parameters.AddWithValue("@designName", txtDesignName.Text);
                cmd.Parameters.AddWithValue("@width", Convert.ToDecimal(tbWidth.Text));
                cmd.Parameters.AddWithValue("@height", Convert.ToDecimal(tbHeight.Text));
                cmd.Parameters.AddWithValue("@fabricArticle",
                    selectedFabric?.Type == "fabric" ? (object)Convert.ToInt32(selectedFabric.Id) : DBNull.Value);
                cmd.Parameters.AddWithValue("@borderType", cbBorder.SelectedItem?.ToString() ?? "");
                cmd.Parameters.AddWithValue("@borderColor", cbBorderColor.SelectedItem?.ToString() ?? "");
                cmd.Parameters.AddWithValue("@borderWidth", Convert.ToDecimal(tbBorderWidth.Text));

                return (int)cmd.ExecuteScalar();
            }
        }

        private void SaveDesignAccessories(NpgsqlConnection connection, NpgsqlTransaction transaction, int designId)
        {
            // Удаляем старые записи
            string deleteQuery = "DELETE FROM design_accessories WHERE design_id = @designId";
            using (var deleteCmd = new NpgsqlCommand(deleteQuery, connection, transaction))
            {
                deleteCmd.Parameters.AddWithValue("@designId", designId);
                deleteCmd.ExecuteNonQuery();
            }

            // Добавляем новые записи
            string insertQuery = @"
                INSERT INTO design_accessories 
                (design_id, accessory_article, position_x, position_y, width, height, rotation, layer_order)
                VALUES (@designId, @accessoryArticle, @positionX, @positionY, @width, @height, @rotation, @layerOrder)";

            int layerOrder = 1;
            foreach (var accessory in designAccessories)
            {
                using (var cmd = new NpgsqlCommand(insertQuery, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@designId", designId);
                    cmd.Parameters.AddWithValue("@accessoryArticle", accessory.AccessoryArticle);
                    cmd.Parameters.AddWithValue("@positionX", (decimal)accessory.X);
                    cmd.Parameters.AddWithValue("@positionY", (decimal)accessory.Y);
                    cmd.Parameters.AddWithValue("@width", (decimal)accessory.Width);
                    cmd.Parameters.AddWithValue("@height", (decimal)accessory.Height);
                    cmd.Parameters.AddWithValue("@rotation", (decimal)accessory.Rotation);
                    cmd.Parameters.AddWithValue("@layerOrder", layerOrder++);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ======================= УПРАВЛЕНИЕ ВИДОМ =======================

        private void SliderZoom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (DesignCanvas != null)
            {
                var scaleTransform = new ScaleTransform(sliderZoom.Value, sliderZoom.Value);
                DesignCanvas.RenderTransform = scaleTransform;
                lblZoom.Text = $"{sliderZoom.Value * 100:F0}%";
            }
        }

        private void BtnResetView_Click(object sender, RoutedEventArgs e)
        {
            sliderZoom.Value = 1.0;
            CanvasScrollViewer.ScrollToHorizontalOffset(0);
            CanvasScrollViewer.ScrollToVerticalOffset(0);
        }

        private void BtnFitToSize_Click(object sender, RoutedEventArgs e)
        {
            double canvasWidth = DesignCanvas.ActualWidth;
            double canvasHeight = DesignCanvas.ActualHeight;
            double viewerWidth = CanvasScrollViewer.ActualWidth;
            double viewerHeight = CanvasScrollViewer.ActualHeight;

            double scaleX = viewerWidth / canvasWidth;
            double scaleY = viewerHeight / canvasHeight;
            double scale = Math.Min(scaleX, scaleY) * 0.9; // 90% для отступов

            sliderZoom.Value = Math.Max(0.1, Math.Min(3.0, scale));
        }
    }

    // ======================= МОДЕЛИ ДАННЫХ =======================

    public class MaterialItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ImagePath { get; set; }
        public string Type { get; set; }
    }

    public class DesignAccessoryItem : INotifyPropertyChanged
    {
        private string _position;

        public string Id { get; set; }
        public string AccessoryArticle { get; set; }
        public string Name { get; set; }
        public string ImagePath { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Rotation { get; set; }
        public UIElement CanvasElement { get; set; }

        public string Position
        {
            get => _position;
            set
            {
                _position = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
