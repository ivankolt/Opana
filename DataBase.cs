    // File: DataBase.cs
    using Npgsql;
    using NpgsqlTypes;
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Windows; // Уберем эту зависимость, если возможно

    namespace UchPR
    {
        internal class DataBase
        {
            public string connectionString = "Host=localhost;Username=postgres;Password=12345;Database=UchPR";

            /// <summary>
            /// Проверяет учетные данные пользователя и возвращает его роль.
            /// </summary>
            /// <returns>Роль пользователя или null, если аутентификация не удалась.</returns>
            public string AuthenticateUser(string login, string password)
            {
                string role = null;
                try
                {
                    using (var conn = new NpgsqlConnection(connectionString))
                    {
                        conn.Open();
                        var sql = "SELECT role FROM public.users WHERE login = @login AND password = @password";
                        using (var cmd = new NpgsqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("login", login);
                            cmd.Parameters.AddWithValue("password", password);
                            var result = cmd.ExecuteScalar();
                            if (result != null)
                            {
                                role = result.ToString();
                            }
                        }
                    }
                }
                catch (NpgsqlException)
                {
                    // Просто возвращаем null в случае ошибки. UI сам решит, что показать.
                    return null;
                }
                return role;
            }

            public string GetUserName(string login)
            {
                string name = login; // По умолчанию, если имя не найдено
                try
                {
                    using (var conn = new NpgsqlConnection(connectionString))
                    {
                        conn.Open();
                        var sql = "SELECT name FROM public.users WHERE login = @login";
                        using (var cmd = new NpgsqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("login", login);
                            var result = cmd.ExecuteScalar();
                            if (result != null && result != DBNull.Value)
                            {
                                name = result.ToString();
                            }
                        }
                    }
                }
                catch (NpgsqlException)
                {
                    // В случае ошибки просто вернем логин
                }
                return name;
            }
            public decimal GetConversionFactor(string article, int fromUnitId, int toUnitId)
            {
                // Если единицы совпадают, коэффициент равен 1
                if (fromUnitId == toUnitId) return 1.0m;

                decimal factor = 1.0m; // Значение по умолчанию, если правило не найдено
                var sql = @"
                    SELECT conversion_factor FROM public.UnitConversionRules 
                    WHERE material_article = @article 
                      AND from_unit_id = @fromUnitId 
                      AND to_unit_id = @toUnitId;
                ";

                try
                {
                    using (var conn = new NpgsqlConnection(connectionString))
                    {
                        conn.Open();
                        using (var cmd = new NpgsqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("article", article);
                            cmd.Parameters.AddWithValue("fromUnitId", fromUnitId);
                            cmd.Parameters.AddWithValue("toUnitId", toUnitId);

                            // ExecuteScalar используется для запросов, возвращающих одно значение
                            var result = cmd.ExecuteScalar();

                            if (result != null && result != System.DBNull.Value)
                            {
                                factor = System.Convert.ToDecimal(result);
                            }
                        }
                    }
                }
                catch (NpgsqlException ex)
                {
                    MessageBox.Show("Ошибка при получении коэффициента пересчета: " + ex.Message);
                }

                return factor;
            }

            public List<MaterialStockItem> GetFabricStock()
            {
                var stockItems = new List<MaterialStockItem>();

                try
                {
                    string query = @"
                SELECT 
                    fw.fabric_article AS Article,
                    fn.name AS Name,
                    SUM(fw.length * fw.width) AS BaseQuantity,
                    SUM(fw.total_cost) AS TotalCost,
                    f.unit_of_measurement_id AS BaseUnitId
                FROM fabricwarehouse fw
                LEFT JOIN fabric f ON fw.fabric_article = f.article
                LEFT JOIN fabricname fn ON f.name_id = fn.code
                WHERE fn.name IS NOT NULL
                GROUP BY fw.fabric_article, fn.name, f.unit_of_measurement_id
                ORDER BY fw.fabric_article";

                    var data = GetData(query);

                    foreach (DataRow row in data.Rows)
                    {
                        stockItems.Add(new MaterialStockItem
                        {
                            Article = row["Article"].ToString(),
                            Name = row["Name"].ToString(),
                            BaseQuantity = Convert.ToDecimal(row["BaseQuantity"]),
                            TotalCost = Convert.ToDecimal(row["TotalCost"]),
                            BaseUnitId = Convert.ToInt32(row["BaseUnitId"])
                        });
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка загрузки данных склада тканей: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"Ошибка GetFabricStock: {ex.Message}");
                }

                return stockItems;
            }

            public List<UnitOfMeasurement> GetAllUnits()
            {
                var units = new List<UnitOfMeasurement>();
                var sql = "SELECT code, name FROM public.UnitOfMeasurement ORDER BY name;";

                try
                {
                    using (var conn = new NpgsqlConnection(connectionString))
                    {
                        conn.Open();
                        using (var cmd = new NpgsqlCommand(sql, conn))
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                units.Add(new UnitOfMeasurement
                                {
                                    Code = reader.GetInt32(0),
                                    Name = reader.GetString(1)
                                });
                            }
                        }
                    }
                }
                catch (NpgsqlException ex)
                {
                    MessageBox.Show("Ошибка при получении единиц измерения: " + ex.Message);
                }
                return units;
            }

            /// <summary>
            /// Проверяет, существует ли пользователь с таким логином в базе.
            /// </summary>
            /// <returns>true, если логин занят, иначе false.</returns>
            public bool LoginExists(string login)
            {
                try
                {
                    using (var conn = new NpgsqlConnection(connectionString))
                    {
                        conn.Open();
                        var sql = "SELECT COUNT(1) FROM public.users WHERE login = @login";
                        using (var cmd = new NpgsqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("login", login);
                            return (long)cmd.ExecuteScalar() > 0;
                        }
                    }
                }
                catch (NpgsqlException)
                {
                    // В случае ошибки с БД лучше считать, что логин существует,
                    // чтобы избежать создания дубликата.
                    return true;
                }
            }

            /// <summary>
            /// Регистрирует нового пользователя с ролью "Заказчик".
            /// </summary>
            /// <returns>true, если пользователь успешно создан, иначе false.</returns>
            public bool RegisterUser(string name, string login, string password)
            {
                try
                {
                    using (var conn = new NpgsqlConnection(connectionString))
                    {
                        conn.Open();
                        var sql = "INSERT INTO public.users (login, password, role, name) VALUES (@login, @password, 'Заказчик', @name)";
                        using (var cmd = new NpgsqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("login", login);
                            cmd.Parameters.AddWithValue("password", password);
                            cmd.Parameters.AddWithValue("name", name);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    return true; // Успешно
                }
                catch (NpgsqlException)
                {
                    return false; // Ошибка
                }
            }
            public List<ProductDisplayItem> GetProducts()
            {
                var products = new List<ProductDisplayItem>();
                var sql = @"
            SELECT p.article, pn.name, p.width, p.length, p.image, p.comment
            FROM Product p
            JOIN ProductName pn ON p.name_id = pn.id
            ORDER BY pn.name;";

                return products;
            }

            public List<ThresholdSettingsItem> GetMaterialsForThresholdSettings(string materialType)
            {
                var items = new List<ThresholdSettingsItem>();
                string sql;

                if (materialType == "Fabric")
                {
                    sql = @"
                SELECT f.article, fn.name AS MaterialName, 
                       COALESCE(f.scrap_threshold, 0) AS ScrapThreshold, 
                       u.name AS UnitName, u.code AS UnitId
                FROM Fabric f
                JOIN FabricName fn ON f.name_id = fn.code
                JOIN UnitOfMeasurement u ON f.unit_of_measurement_id = u.code
                ORDER BY fn.name;";
                }
                else
                {
                    sql = @"
                SELECT a.article, fan.name AS MaterialName, 
                       COALESCE(a.scrap_threshold, 0) AS ScrapThreshold, 
                       u.name AS UnitName, u.code AS UnitId
                FROM Accessory a
                JOIN FurnitureAccessoryName fan ON a.name_id = fan.id
                JOIN UnitOfMeasurement u ON a.unit_of_measurement_id = u.code
                ORDER BY fan.name;";
                }

                try
                {
                    using (var conn = new NpgsqlConnection(connectionString))
                    {
                        conn.Open();
                        using (var cmd = new NpgsqlCommand(sql, conn))
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                items.Add(new ThresholdSettingsItem
                                {
                                    // ИСПРАВЛЕНО: Используем правильные типы данных
                                    Article = reader.GetInt32(0),              // article (INTEGER → string)
                                    MaterialName = reader.GetString(1),                   // MaterialName (VARCHAR)
                                    ScrapThreshold = reader.GetDecimal(2),                // ScrapThreshold (DECIMAL)
                                    UnitName = reader.GetString(3),                       // UnitName (VARCHAR)
                                    UnitId = reader.GetInt32(4)                          // UnitId (INTEGER)
                                });
                            }
                        }
                    }
                }
                catch (NpgsqlException ex)
                {
                    MessageBox.Show("Ошибка при получении материалов для настройки: " + ex.Message);
                }
                return items;
            }

            // File: DataBase.cs
            public bool UpdateScrapThreshold(int article, decimal threshold, string materialType)
            {
                string sql = materialType == "Fabric"
                    ? "UPDATE Fabric SET scrap_threshold = @threshold WHERE article = @article"
                    : "UPDATE Accessory SET scrap_threshold = @threshold WHERE article = @article";

                try
                {
                    using (var conn = new NpgsqlConnection(connectionString))
                    {
                        conn.Open();
                        using (var cmd = new NpgsqlCommand(sql, conn))
                        {
                            // Теперь оба параметра имеют правильные типы
                            cmd.Parameters.Add("threshold", NpgsqlDbType.Integer).Value = (int)Math.Round(threshold);
                            cmd.Parameters.Add("article", NpgsqlDbType.Integer).Value = article; // Теперь article уже int

                            int rowsAffected = cmd.ExecuteNonQuery();
                            return rowsAffected > 0;
                        }
                    }
                }
                catch (NpgsqlException ex)
                {
                    MessageBox.Show($"Ошибка при сохранении порога списания для артикула {article}: {ex.Message}");
                    return false;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Общая ошибка при сохранении: {ex.Message}");
                    return false;
                }
            }

            public List<ScrapLogItem> GetScrapLog(DateTime? fromDate = null, DateTime? toDate = null)
            {
                var logItems = new List<ScrapLogItem>();
                string sql = @"
            SELECT sl.log_date, sl.material_article, sl.quantity_scrapped, 
                   sl.cost_scrapped, u.name AS UnitName,
                   COALESCE(sl.written_off_by, 'Система') AS WrittenOffBy,
                   CASE 
                       WHEN EXISTS(SELECT 1 FROM Fabric WHERE article::TEXT = sl.material_article) 
                       THEN (SELECT fn.name FROM Fabric f 
                             JOIN FabricName fn ON f.name_id = fn.code 
                             WHERE f.article::TEXT = sl.material_article)
                       ELSE (SELECT fan.name FROM Accessory a 
                             JOIN FurnitureAccessoryName fan ON a.name_id = fan.id 
                             WHERE a.article::TEXT = sl.material_article)
                   END AS MaterialName
            FROM ScrapLog sl
            JOIN UnitOfMeasurement u ON sl.unit_of_measurement_id = u.code
            WHERE (@fromDate IS NULL OR sl.log_date >= @fromDate)
              AND (@toDate IS NULL OR sl.log_date <= @toDate)
            ORDER BY sl.log_date DESC;";

                try
                {
                    using (var conn = new NpgsqlConnection(connectionString))
                    {
                        conn.Open();
                        using (var cmd = new NpgsqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("fromDate", (object)fromDate ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("toDate", (object)toDate ?? DBNull.Value);

                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    logItems.Add(new ScrapLogItem
                                    {
                                        LogDate = reader.GetDateTime(0),                    // log_date
                                        MaterialArticle = reader.GetString(1),             // material_article
                                        QuantityScrap = reader.GetDecimal(2),              // quantity_scrapped
                                        CostScrap = reader.GetDecimal(3),                  // cost_scrapped
                                        UnitName = reader.GetString(4),                    // UnitName
                                        WrittenOffBy = reader.GetString(5),                // WrittenOffBy
                                        MaterialName = reader.IsDBNull(6) ? "Неизвестно" : reader.GetString(6) // MaterialName
                                    });
                                }
                            }
                        }
                    }
                }
                catch (NpgsqlException ex)
                {
                    MessageBox.Show("Ошибка при получении журнала списаний: " + ex.Message);
                }
                return logItems;
            }


            public DataTable GetData(string query, NpgsqlParameter[] parameters = null)
            {
                DataTable dataTable = new DataTable();

                try
                {
                    using (var conn = new NpgsqlConnection(connectionString))
                    {
                        conn.Open();
                        using (var command = new NpgsqlCommand(query, conn))
                        {
                            if (parameters != null)
                            {
                                command.Parameters.AddRange(parameters);
                            }

                            using (var adapter = new NpgsqlDataAdapter(command))
                            {
                                adapter.Fill(dataTable);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Ошибка выполнения запроса: {ex.Message}");
                }

                return dataTable;
            }

            public int ExecuteQuery(string query, NpgsqlParameter[] parameters = null)
            {
                int result = 0;

                try
                {
                    using (var conn = new NpgsqlConnection(connectionString))
                    {
                        conn.Open();
                        using (var command = new NpgsqlCommand(query, conn))
                        {
                            if (parameters != null)
                            {
                                command.Parameters.AddRange(parameters);
                            }

                            result = command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Ошибка выполнения команды: {ex.Message}");
                }

                return result;
            }

            public bool WriteOffMaterial(string materialArticle, decimal quantityToWriteOff, string materialType, string userLogin)
            {
                try
                {
                    using (var conn = new NpgsqlConnection(connectionString))
                    {
                        conn.Open();
                        using (var transaction = conn.BeginTransaction())
                        {
                            try
                            {
                                // Получаем текущий остаток
                                decimal currentQuantity = GetCurrentQuantity(materialArticle, materialType, conn);
                                decimal newQuantity = currentQuantity - quantityToWriteOff;

                                // Выполняем списание
                                string updateSql = materialType == "Fabric"
                                    ? "UPDATE FabricWarehouse SET length = length - @quantity WHERE fabric_article = @article"
                                    : "UPDATE AccessoryWarehouse SET quantity = quantity - @quantity WHERE accessory_article = @article";

                                using (var cmd = new NpgsqlCommand(updateSql, conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("quantity", quantityToWriteOff);
                                    cmd.Parameters.AddWithValue("article", materialArticle);
                                    cmd.ExecuteNonQuery();
                                }


                                ProcessAutoScrapWithTransaction(materialArticle, newQuantity, materialType, userLogin, conn, transaction);

                                transaction.Commit();
                                return true;
                            }
                            catch
                            {
                                transaction.Rollback();
                                throw;
                            }
                        }
                    }
                }
                catch (NpgsqlException ex)
                {
                    MessageBox.Show("Ошибка при списании материала: " + ex.Message);
                    return false;
                }
            }


            private decimal GetCurrentQuantity(string materialArticle, string materialType, NpgsqlConnection conn)
            {
                string sql = materialType == "Fabric"
                    ? "SELECT COALESCE(SUM(length), 0) FROM FabricWarehouse WHERE fabric_article = @article"
                    : "SELECT COALESCE(SUM(quantity), 0) FROM AccessoryWarehouse WHERE accessory_article = @article";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("article", materialArticle);
                    return Convert.ToDecimal(cmd.ExecuteScalar());
                }
            }

            private void ProcessAutoScrapWithTransaction(string materialArticle, decimal remainingQuantity,
        string materialType, string userLogin, NpgsqlConnection conn, NpgsqlTransaction transaction)
            {
                try
                {
                    // Получаем порог списания для материала
                    string thresholdSql = materialType == "Fabric"
                        ? "SELECT COALESCE(scrap_threshold, 0), unit_of_measurement_id FROM Fabric WHERE article = @article"
                        : "SELECT COALESCE(scrap_threshold, 0), unit_of_measurement_id FROM Accessory WHERE article = @article";

                    decimal threshold = 0;
                    int unitId = 0;

                    using (var cmd = new NpgsqlCommand(thresholdSql, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("article", materialArticle);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                threshold = reader.GetDecimal(0);        // scrap_threshold
                                unitId = reader.GetInt32(1);             // unit_of_measurement_id
                            }
                        }
                    }

                    // Если остаток меньше порога и порог установлен
                    if (threshold > 0 && remainingQuantity > 0 && remainingQuantity <= threshold)
                    {
                        // Рассчитываем среднюю стоимость
                        decimal avgCost = GetAverageCostForMaterialWithTransaction(materialArticle, materialType, conn, transaction);
                        decimal scrapCost = remainingQuantity * avgCost;

                        // Записываем в журнал списаний
                        string insertSql = @"
                    INSERT INTO ScrapLog (material_article, quantity_scrapped, unit_of_measurement_id, cost_scrapped, written_off_by)
                    VALUES (@article, @quantity, @unitId, @cost, @user)";

                        using (var cmd = new NpgsqlCommand(insertSql, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("article", materialArticle);
                            cmd.Parameters.AddWithValue("quantity", remainingQuantity);
                            cmd.Parameters.AddWithValue("unitId", unitId);
                            cmd.Parameters.AddWithValue("cost", scrapCost);
                            cmd.Parameters.AddWithValue("user", (object)userLogin ?? DBNull.Value);
                            cmd.ExecuteNonQuery();
                        }

                        // Обнуляем остаток на складе (списываем полностью)
                        string updateSql = materialType == "Fabric"
                            ? "UPDATE FabricWarehouse SET length = 0, total_cost = 0 WHERE fabric_article = @article AND length > 0"
                            : "UPDATE AccessoryWarehouse SET quantity = 0, total_cost = 0 WHERE accessory_article = @article AND quantity > 0";

                        using (var cmd = new NpgsqlCommand(updateSql, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("article", materialArticle);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception ex)
                {
                    // В рамках транзакции не показываем MessageBox, а пробрасываем исключение
                    throw new Exception($"Ошибка при автоматическом списании обрезков: {ex.Message}", ex);
                }
            }

            private decimal GetAverageCostForMaterialWithTransaction(string materialArticle, string materialType,
        NpgsqlConnection conn, NpgsqlTransaction transaction)
            {
                string sql = materialType == "Fabric"
                    ? "SELECT CASE WHEN SUM(length) > 0 THEN SUM(total_cost) / SUM(length) ELSE 0 END FROM FabricWarehouse WHERE fabric_article = @article"
                    : "SELECT CASE WHEN SUM(quantity) > 0 THEN SUM(total_cost) / SUM(quantity) ELSE 0 END FROM AccessoryWarehouse WHERE accessory_article = @article";

                using (var cmd = new NpgsqlCommand(sql, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("article", materialArticle);
                    var result = cmd.ExecuteScalar();
                    return result == null || result == DBNull.Value ? 0 : Convert.ToDecimal(result);
                }
            }


            public void ProcessAutoScrap(string materialArticle, decimal remainingQuantity, string materialType, string userLogin)
            {
                try
                {
                    using (var conn = new NpgsqlConnection(connectionString))
                    {
                        conn.Open();

                        // Получаем порог списания для материала
                        string thresholdSql = materialType == "Fabric"
                            ? "SELECT scrap_threshold, unit_of_measurement_id FROM Fabric WHERE article = @article"
                            : "SELECT scrap_threshold, unit_of_measurement_id FROM Accessory WHERE article = @article";

                        decimal threshold = 0;
                        int unitId = 0;

                        using (var cmd = new NpgsqlCommand(thresholdSql, conn))
                        {
                            cmd.Parameters.AddWithValue("article", materialArticle);
                            using (var reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    threshold = reader.IsDBNull(0) ? 0 : reader.GetDecimal(0);    // scrap_threshold
                                    unitId = reader.GetInt32(1);                                   // unit_of_measurement_id
                                }
                            }
                        }

                        // Если остаток меньше порога и порог установлен
                        if (threshold > 0 && remainingQuantity < threshold)
                        {
                            // Рассчитываем среднюю стоимость
                            decimal avgCost = GetAverageCostForMaterial(materialArticle, materialType, conn);
                            decimal scrapCost = remainingQuantity * avgCost;

                            // Записываем в журнал списаний
                            string insertSql = @"
                        INSERT INTO ScrapLog (material_article, quantity_scrapped, unit_of_measurement_id, cost_scrapped, written_off_by)
                        VALUES (@article, @quantity, @unitId, @cost, @user)";

                            using (var cmd = new NpgsqlCommand(insertSql, conn))
                            {
                                cmd.Parameters.AddWithValue("article", materialArticle);
                                cmd.Parameters.AddWithValue("quantity", remainingQuantity);
                                cmd.Parameters.AddWithValue("unitId", unitId);
                                cmd.Parameters.AddWithValue("cost", scrapCost);
                                cmd.Parameters.AddWithValue("user", userLogin);
                                cmd.ExecuteNonQuery();
                            }

                            // Обнуляем остаток на складе
                            string updateSql = materialType == "Fabric"
                                ? "UPDATE FabricWarehouse SET length = 0, total_cost = 0 WHERE fabric_article = @article"
                                : "UPDATE AccessoryWarehouse SET quantity = 0, total_cost = 0 WHERE accessory_article = @article";

                            using (var cmd = new NpgsqlCommand(updateSql, conn))
                            {
                                cmd.Parameters.AddWithValue("article", materialArticle);
                                cmd.ExecuteNonQuery();
                            }
                        }
                    }
                }
                catch (NpgsqlException ex)
                {
                    MessageBox.Show("Ошибка при автоматическом списании: " + ex.Message);
                }
            }

            private decimal GetAverageCostForMaterial(string materialArticle, string materialType, NpgsqlConnection conn)
            {
                string sql = materialType == "Fabric"
                    ? "SELECT CASE WHEN SUM(length) > 0 THEN SUM(total_cost) / SUM(length) ELSE 0 END FROM FabricWarehouse WHERE fabric_article = @article"
                    : "SELECT CASE WHEN SUM(quantity) > 0 THEN SUM(total_cost) / SUM(quantity) ELSE 0 END FROM AccessoryWarehouse WHERE accessory_article = @article";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("article", materialArticle);
                    var result = cmd.ExecuteScalar();
                    return result == DBNull.Value ? 0 : Convert.ToDecimal(result);
                }
            }
            public decimal GetAverageFabricPrice(int fabricArticle)
            {
                string query = @"
            SELECT 
                CASE 
                    WHEN SUM(length * width) > 0 
                    THEN SUM(total_cost) / SUM(length * width)
                    ELSE 0 
                END as average_price
            FROM fabricwarehouse 
            WHERE fabric_article = @fabricArticle";

                var parameters = new NpgsqlParameter[] {
            new NpgsqlParameter("@fabricArticle", fabricArticle)
        };

                var result = GetScalarValue(query, parameters);
                return result != null ? Convert.ToDecimal(result) : 0m;
            }

            public decimal GetAverageAccessoryPrice(string accessoryArticle)
            {
                string query = @"
            SELECT 
                CASE 
                    WHEN SUM(quantity) > 0 
                    THEN SUM(total_cost) / SUM(quantity)
                    ELSE 0 
                END as average_price
            FROM accessorywarehouse 
            WHERE accessory_article = @accessoryArticle";

                var parameters = new NpgsqlParameter[] {
            new NpgsqlParameter("@accessoryArticle", accessoryArticle)
        };

                var result = GetScalarValue(query, parameters);
                return result != null ? Convert.ToDecimal(result) : 0m;
            }

            public decimal CalculateFabricWriteoffCost(int fabricArticle, decimal quantityToWriteoff)
            {
                decimal averagePrice = GetAverageFabricPrice(fabricArticle);
                return averagePrice * quantityToWriteoff;
            }

            public decimal CalculateAccessoryWriteoffCost(string accessoryArticle, int quantityToWriteoff)
            {
                decimal averagePrice = GetAverageAccessoryPrice(accessoryArticle);
                return averagePrice * quantityToWriteoff;
            }


            public object GetScalarValue(string query, NpgsqlParameter[] parameters = null)
            {
                object result = null;

                try
                {
                    using (var conn = new NpgsqlConnection(connectionString))
                    {
                        conn.Open();
                        using (var command = new NpgsqlCommand(query, conn))
                        {
                            if (parameters != null)
                            {
                                command.Parameters.AddRange(parameters);
                            }

                            result = command.ExecuteScalar();
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Ошибка выполнения скалярного запроса: {ex.Message}");
                }

                return result;
            }

            public NpgsqlConnection GetConnection()
            {
                return new NpgsqlConnection(connectionString);
            }
            public List<ProductCompositionItem> GetProductComposition(string productArticle)
            {
                var composition = new List<ProductCompositionItem>();
                // Сложный запрос, который объединяет данные из двух таблиц:
                // тканевый состав и фурнитурный состав.
                var sql = @"
            -- Получаем ткани
            SELECT 'Ткань' AS MaterialType, fn.name, fp.quantity, uom.name AS UnitName
            FROM FabricProducts fp
            JOIN Fabric f ON fp.fabric_article = f.article
            JOIN FabricName fn ON f.name_id = fn.code
            JOIN UnitOfMeasurement uom ON f.unit_of_measurement_id = uom.code
            WHERE fp.product_article = @article
        
            UNION ALL
        
            -- Получаем фурнитуру
            SELECT 'Фурнитура' AS MaterialType, fan.name, ap.quantity, uom.name AS UnitName
            FROM AccessoryProducts ap
            JOIN Accessory a ON ap.accessory_article = a.article
            JOIN FurnitureAccessoryName fan ON a.name_id = fan.id
            JOIN UnitOfMeasurement uom ON ap.unit_of_measurement_id = uom.code
            WHERE ap.product_article = @article;
        ";

                // ... здесь код выполнения запроса с параметром @article,
                // который заполняет список `composition` ...

                return composition;
            }


        public List<FabricWarehouseItem> GetFabricWarehouseData()
        {
            var fabricItems = new List<FabricWarehouseItem>();

            try
            {
                string query = @"
            SELECT 
                fw.roll,
                fw.fabric_article,
                COALESCE(fn.name, 'Неизвестная ткань') AS fabric_name,
                fw.width,
                fw.length,
                fw.purchase_price,
                fw.total_cost
            FROM fabricwarehouse fw
            LEFT JOIN fabric f ON fw.fabric_article = f.article
            LEFT JOIN fabricname fn ON f.name_id = fn.code
            WHERE fw.width > 0 AND fw.length > 0
            ORDER BY fw.roll, fw.fabric_article";

                var data = GetData(query);

                foreach (DataRow row in data.Rows)
                {
                    fabricItems.Add(new FabricWarehouseItem
                    {
                        Roll = Convert.ToInt32(row["roll"]),
                        FabricArticle = Convert.ToInt32(row["fabric_article"]),
                        FabricName = row["fabric_name"].ToString(),
                        Width = Convert.ToDecimal(row["width"]),
                        Length = Convert.ToDecimal(row["length"]),
                        PurchasePrice = Convert.ToDecimal(row["purchase_price"]),
                        TotalCost = Convert.ToDecimal(row["total_cost"])
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных склада тканей: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Ошибка GetFabricWarehouseData: {ex.Message}");
            }

            return fabricItems;
        }

        public List<AccessoryWarehouseItem> GetAccessoryWarehouseData()
        {
            var accessoryItems = new List<AccessoryWarehouseItem>();

            try
            {
                string query = @"
            SELECT 
                aw.batch_number,
                aw.accessory_article,
                COALESCE(fan.name, 'Неизвестная фурнитура') AS accessory_name,
                aw.quantity,
                COALESCE(uom.name, 'шт') AS unit_name,
                aw.purchase_price,
                aw.total_cost
            FROM accessorywarehouse aw
            LEFT JOIN accessory a ON aw.accessory_article = a.article
            LEFT JOIN furnitureaccessoryname fan ON a.name_id = fan.id
            LEFT JOIN unitofmeasurement uom ON a.unit_of_measurement_id = uom.code
            WHERE aw.quantity > 0
            ORDER BY aw.batch_number, aw.accessory_article";

                var data = GetData(query);

                foreach (DataRow row in data.Rows)
                {
                    accessoryItems.Add(new AccessoryWarehouseItem
                    {
                        BatchNumber = Convert.ToInt32(row["batch_number"]),
                        AccessoryArticle = row["accessory_article"].ToString(),
                        AccessoryName = row["accessory_name"].ToString(),
                        Quantity = Convert.ToInt32(row["quantity"]),
                        UnitName = row["unit_name"].ToString(),
                        PurchasePrice = Convert.ToDecimal(row["purchase_price"]),
                        TotalCost = Convert.ToDecimal(row["total_cost"])
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных склада фурнитуры: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Ошибка GetAccessoryWarehouseData: {ex.Message}");
            }

            return accessoryItems;
        }


        public List<MaterialStockItem> GetAccessoryStock()
            {
                var stockItems = new List<MaterialStockItem>();

                try
                {
                    string query = @"
                SELECT 
                    aw.accessory_article AS Article,
                    COALESCE(fan.name, 'Неизвестная фурнитура') AS Name,
                    SUM(aw.quantity) AS BaseQuantity,
                    SUM(aw.total_cost) AS TotalCost,
                    COALESCE(a.unit_of_measurement_id, 1) AS BaseUnitId
                FROM accessorywarehouse aw
                LEFT JOIN accessory a ON aw.accessory_article = a.article
                LEFT JOIN furnitureaccessoryname fan ON a.name_id = fan.id
                WHERE aw.quantity > 0
                GROUP BY aw.accessory_article, fan.name, a.unit_of_measurement_id
                ORDER BY fan.name";

                    var data = GetData(query);

                    foreach (DataRow row in data.Rows)
                    {
                        stockItems.Add(new MaterialStockItem
                        {
                            Article = row["Article"].ToString(),
                            Name = row["Name"].ToString(),
                            BaseQuantity = Convert.ToDecimal(row["BaseQuantity"]),
                            TotalCost = Convert.ToDecimal(row["TotalCost"]),
                            BaseUnitId = Convert.ToInt32(row["BaseUnitId"])
                        });
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка загрузки данных склада фурнитуры: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"Ошибка GetAccessoryStock: {ex.Message}");
                }

                return stockItems;
            }


        }
    }
