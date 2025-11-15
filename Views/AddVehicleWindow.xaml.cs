using System;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace Kursovaya.Views
{
    public partial class AddVehicleWindow : Window
    {
        private readonly user149_dbEntities _db = new user149_dbEntities();
        public Vehicles NewVehicle { get; private set; }

    
        private readonly string[] _colors = {
            "Белый","Черный","Серый","Серебристый","Синий","Красный","Зеленый","Бежевый",
            "Коричневый","Желтый","Оранжевый","Фиолетовый"
        };
        private readonly string[] _fuel = { "Бензин", "Дизель", "Гибрид", "Электро" };
        private readonly string[] _trans = { "МКПП", "АКПП", "Вариатор", "Робот" };

        private readonly string[] _allowedStatuses = { "В наличии", "В резерве", "Продано" };

        private const string SalonClientName = "[СИСТЕМА] Автосалон";

        public AddVehicleWindow()
        {
            InitializeComponent();
            Closed += (s, e) => _db.Dispose();

            var years = Enumerable.Range(1990, DateTime.Now.Year - 1990 + 1).Reverse().ToList();
            YearCombo.ItemsSource = years;
            ColorCombo.ItemsSource = _colors;
            FuelCombo.ItemsSource = _fuel;
            TransmissionCombo.ItemsSource = _trans;

            StatusCombo.ItemsSource = _allowedStatuses;
            StatusCombo.SelectedItem = _allowedStatuses.First();
            YearCombo.SelectedItem = DateTime.Now.Year;
        }

        private Clients GetOrCreateSalonClient()
        {
         
            var salon = _db.Clients.FirstOrDefault(c => c.FullName == SalonClientName);
            if (salon != null) return salon;

            salon = new Clients { FullName = SalonClientName, CreatedAt = DateTime.Now };
            _db.Clients.Add(salon);
            _db.SaveChanges();
            return salon;
        }

        private void OnChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => ValidateForm();
        private void OnChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => ValidateForm();

        private void ValidateForm()
        {
         
            bool required =
                !string.IsNullOrWhiteSpace(VinBox.Text) &&
                !string.IsNullOrWhiteSpace(BrandBox.Text) &&
                !string.IsNullOrWhiteSpace(ModelBox.Text) &&
                YearCombo.SelectedItem != null &&
                !string.IsNullOrWhiteSpace(PriceBox.Text) &&
                StatusCombo.SelectedItem != null;

            AddButton.IsEnabled = required;
        }

        private void DigitsOnly(object sender, TextCompositionEventArgs e) => e.Handled = !e.Text.All(char.IsDigit);
        private void DecimalOnly(object sender, TextCompositionEventArgs e)
        {
            var ch = e.Text;
            e.Handled = !(char.IsDigit(ch[0]) || ch == "," || ch == ".");
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var vin = (VinBox.Text ?? "").Trim().ToUpperInvariant();
                var plate = (PlateBox.Text ?? "").Trim().ToUpperInvariant();
                var brand = (BrandBox.Text ?? "").Trim();
                var model = (ModelBox.Text ?? "").Trim();
                var color = (ColorCombo.SelectedItem as string) ?? "";
                var status = (StatusCombo.SelectedItem as string) ?? "";
                var fuel = (FuelCombo.SelectedItem as string) ?? "";
                var trans = (TransmissionCombo.SelectedItem as string) ?? "";
                var year = (int)(YearCombo.SelectedItem ?? DateTime.Now.Year);

            
                if (vin.Length < 3)
                {
                    MessageBox.Show("Введите корректный VIN (минимум 3 символа).", "Автомобиль",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!decimal.TryParse(PriceBox.Text.Replace(" ", ""), NumberStyles.Any, CultureInfo.CurrentCulture, out var price) || price <= 0m)
                {
                    MessageBox.Show("Цена должна быть больше нуля.", "Автомобиль",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (year < 1990 || year > DateTime.Now.Year + 1)
                {
                    MessageBox.Show($"Год должен быть в диапазоне 1990..{DateTime.Now.Year + 1}.", "Автомобиль",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int mileage = 0;
                if (!string.IsNullOrWhiteSpace(MileageBox.Text))
                {
                    if (!int.TryParse(MileageBox.Text, out mileage) || mileage < 0)
                    {
                        MessageBox.Show("Пробег должен быть неотрицательным числом.", "Автомобиль",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                decimal? engineVol = null;
                if (!string.IsNullOrWhiteSpace(EngineVolumeBox.Text))
                {
                    var txt = EngineVolumeBox.Text.Replace(",", ".").Trim();
                    if (decimal.TryParse(txt, NumberStyles.Any, CultureInfo.InvariantCulture, out var ev))
                    {
                        if (ev <= 0m)
                        {
                            MessageBox.Show("Объем двигателя должен быть больше 0.", "Автомобиль",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                        engineVol = ev;
                    }
                    else
                    {
                        MessageBox.Show("Введите корректный объем двигателя.", "Автомобиль",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

           
                if (_db.Vehicles.Any(v => v.VIN == vin))
                {
                    MessageBox.Show("Автомобиль с таким VIN уже существует.", "Автомобиль",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

             
                if (!string.IsNullOrWhiteSpace(plate) && _db.Vehicles.Any(v => v.Plate == plate))
                {
                    MessageBox.Show("Автомобиль с таким гос. номером уже существует.", "Автомобиль",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

              
                if (!_allowedStatuses.Contains(status))
                {
                    MessageBox.Show("Недопустимый статус. Выберите из списка.", "Автомобиль",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                using (var tr = _db.Database.BeginTransaction())
                {
                    var salon = GetOrCreateSalonClient();

                    NewVehicle = new Vehicles
                    {
                        OwnerId = salon.Id,
                        VIN = vin,
                        Plate = plate,
                        Brand = brand,
                        Model = model,
                        Year = year,
                        Color = color,
                        Mileage = mileage,
                        Status = status,
                        Price = price,
                        Transmission = trans,
                        Fuel = fuel,
                        EngineVolume = engineVol, 
                        CreatedAt = DateTime.Now
                    };

                    _db.Vehicles.Add(NewVehicle);
                    _db.SaveChanges();

                    tr.Commit();
                }

                DialogResult = true;
                Close();
            }
            catch (DbUpdateException dbEx)
            {
                MessageBox.Show(BuildSqlErrorDetails(dbEx), "Ошибка БД", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при добавлении автомобиля:\n" + ex.Message, "Автомобиль",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string BuildSqlErrorDetails(Exception ex)
        {
            var sb = new StringBuilder();
            int level = 0;
            var cur = ex;
            while (cur != null && level < 10)
            {
                sb.AppendLine($"[{level}] {cur.GetType().Name}: {cur.Message}");
                if (cur is SqlException sql)
                {
                    sb.AppendLine($" SqlNumber: {sql.Number}");
                    foreach (SqlError err in sql.Errors)
                        sb.AppendLine($"  - {err.Number}: {err.Message}");
                }
                cur = cur.InnerException;
                level++;
            }
            return sb.ToString();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}