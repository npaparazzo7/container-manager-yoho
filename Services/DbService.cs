using Microsoft.Data.Sqlite;
using ContainerManager.Models;

namespace ContainerManager.Services
{
    public class DbService
    {
        private readonly string _connectionString;
        private readonly ILogger<DbService> _logger;

        public DbService(IConfiguration configuration, ILogger<DbService> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string non trovata.");
            _logger = logger;
        }

        public void InitializeDb()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
            CREATE TABLE IF NOT EXISTS containers (
                id                INTEGER PRIMARY KEY AUTOINCREMENT,
                nome              TEXT NOT NULL UNIQUE,
                stato             TEXT NOT NULL,
                mig_uuid          TEXT NOT NULL,
                mig_device_index  INTEGER NOT NULL,
                mig_tipo          TEXT NOT NULL,
                vram_gb           REAL NOT NULL,
                data_creazione    TEXT NOT NULL,
                data_modifica     TEXT NOT NULL
            );";

            command.ExecuteNonQuery();
            _logger.LogInformation("Database inizializzato.");
        }

        public void InsertContainer(ContainerRecord container)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
            INSERT INTO containers 
                (nome, stato, mig_uuid, mig_device_index, mig_tipo, vram_gb, data_creazione, data_modifica)
            VALUES 
                ($nome, $stato, $mig_uuid, $mig_device_index, $mig_tipo, $vram_gb, $data_creazione, $data_modifica);";

            command.Parameters.AddWithValue("$nome", container.Nome);
            command.Parameters.AddWithValue("$stato", container.Stato);
            command.Parameters.AddWithValue("$mig_uuid", container.MigUuid);
            command.Parameters.AddWithValue("$mig_device_index", container.MigDeviceIndex);
            command.Parameters.AddWithValue("$mig_tipo", container.MigTipo);
            command.Parameters.AddWithValue("$vram_gb", container.VramGb);
            command.Parameters.AddWithValue("$data_creazione", container.DataCreazione.ToString("o"));
            command.Parameters.AddWithValue("$data_modifica", container.DataModifica.ToString("o"));

            command.ExecuteNonQuery();
            _logger.LogInformation("Container {Nome} inserito nel DB.", container.Nome);
        }

        public List<ContainerRecord> GetContainers()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
            SELECT id, nome, stato, mig_uuid, mig_device_index, mig_tipo, vram_gb, data_creazione, data_modifica
            FROM containers
            WHERE stato != $stato_deleted
            ORDER BY data_creazione DESC;";

            command.Parameters.AddWithValue("$stato_deleted", ContainerState.Deleted);

            return ReadContainers(command);
        }

        public ContainerRecord? GetContainerByNome(string nome)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
            SELECT id, nome, stato, mig_uuid, mig_device_index, mig_tipo, vram_gb, data_creazione, data_modifica
            FROM containers
            WHERE nome = $nome;";

            command.Parameters.AddWithValue("$nome", nome);

            return ReadContainers(command).FirstOrDefault();
        }

        public List<ContainerRecord> GetContainersAttivi()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
            SELECT id, nome, stato, mig_uuid, mig_device_index, mig_tipo, vram_gb, data_creazione, data_modifica
            FROM containers
            WHERE stato = $stato
            ORDER BY data_creazione DESC;";

            command.Parameters.AddWithValue("$stato", ContainerState.Running);

            return ReadContainers(command);
        }

        public void UpdateStatoContainer(string nome, string stato)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
            UPDATE containers
            SET stato = $stato, data_modifica = $data_modifica
            WHERE nome = $nome;";

            command.Parameters.AddWithValue("$stato", stato);
            command.Parameters.AddWithValue("$data_modifica", DateTime.UtcNow.ToString("o"));
            command.Parameters.AddWithValue("$nome", nome);

            command.ExecuteNonQuery();
            _logger.LogInformation("Container {Nome} → stato aggiornato a {Stato}.", nome, stato);
        }

        public void UpdateMigUuid(string nome, string nuovoUuid)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
            UPDATE containers
            SET mig_uuid = $uuid, data_modifica = $data_modifica
            WHERE nome = $nome
              AND stato != $stato_deleted;";

            command.Parameters.AddWithValue("$uuid", nuovoUuid);
            command.Parameters.AddWithValue("$data_modifica", DateTime.UtcNow.ToString("o"));
            command.Parameters.AddWithValue("$nome", nome);
            command.Parameters.AddWithValue("$stato_deleted", ContainerState.Deleted);

            command.ExecuteNonQuery();
            _logger.LogInformation("MIG device_index {Index} → UUID aggiornato.", nome);
        }

        public void DeleteContainer(string nome)
        {
            UpdateStatoContainer(nome, ContainerState.Deleted);
            _logger.LogInformation("Container {Nome} marcato come DELETED.", nome);
        }

        private static List<ContainerRecord> ReadContainers(SqliteCommand command)
        {
            var result = new List<ContainerRecord>();
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                result.Add(new ContainerRecord
                {
                    Id = reader.GetInt32(0),
                    Nome = reader.GetString(1),
                    Stato = reader.GetString(2),
                    MigUuid = reader.GetString(3),
                    MigDeviceIndex = reader.GetInt32(4),
                    MigTipo = reader.GetString(5),
                    VramGb = reader.GetDouble(6),
                    DataCreazione = DateTime.Parse(reader.GetString(7)),
                    DataModifica = DateTime.Parse(reader.GetString(8)),
                });
            }

            return result;
        }
    }
}
