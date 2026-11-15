using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Text.RegularExpressions;

namespace CodeCrafters_Major_Project_Website.Models
{
    /// <summary>
    /// Links ASP.NET Identity logins to authorized rows in the existing Staff
    /// table and updates staff fields without changing the shared schema.
    /// </summary>
    public sealed class StaffAccountService
    {
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["ConnString"].ConnectionString;

        public sealed class StaffRecord
        {
            public string FirstName { get; set; }
            public string Surname { get; set; }
            public string Email { get; set; }
            public string Phone { get; set; }
            public string Status { get; set; }
            public string Role { get; set; }
        }

        public string FindActiveStaffRole(string email)
        {
            const string sql = @"SELECT TOP (1) staff_role
FROM dbo.Staff
WHERE UPPER(LTRIM(RTRIM(ISNULL(staff_email,'')))) = UPPER(@Email)
  AND UPPER(LTRIM(RTRIM(ISNULL(staff_status,'')))) = 'ACTIVE'
  AND UPPER(LTRIM(RTRIM(ISNULL(staff_role,'')))) NOT IN ('', 'GUEST', 'CUSTOMER')
ORDER BY CASE UPPER(LTRIM(RTRIM(staff_role))) WHEN 'ADMIN' THEN 0 WHEN 'MANAGER' THEN 1 ELSE 2 END;";

            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@Email", SqlDbType.VarChar, 100).Value = (email ?? String.Empty).Trim();
                connection.Open();
                object value = command.ExecuteScalar();
                return value == null || value == DBNull.Value ? null : Convert.ToString(value).Trim();
            }
        }

        public string MatchActiveStaffForRegistration(string email, string firstName, string surname, string phone)
        {
            const string sql = @"SELECT TOP (1) staff_role
FROM dbo.Staff
WHERE UPPER(LTRIM(RTRIM(ISNULL(staff_email,'')))) = UPPER(@Email)
  AND UPPER(LTRIM(RTRIM(ISNULL(staff_First_Name,'')))) = UPPER(@FirstName)
  AND UPPER(LTRIM(RTRIM(ISNULL(staff_Surname,'')))) = UPPER(@Surname)
  AND LTRIM(RTRIM(ISNULL(staff_phone_number,''))) = @Phone
  AND UPPER(LTRIM(RTRIM(ISNULL(staff_status,'')))) = 'ACTIVE'
  AND UPPER(LTRIM(RTRIM(ISNULL(staff_role,'')))) NOT IN ('', 'GUEST', 'CUSTOMER')
ORDER BY CASE UPPER(LTRIM(RTRIM(staff_role))) WHEN 'ADMIN' THEN 0 WHEN 'MANAGER' THEN 1 ELSE 2 END;";

            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@Email", SqlDbType.VarChar, 100).Value = (email ?? String.Empty).Trim();
                command.Parameters.Add("@FirstName", SqlDbType.VarChar, 50).Value = (firstName ?? String.Empty).Trim();
                command.Parameters.Add("@Surname", SqlDbType.VarChar, 50).Value = (surname ?? String.Empty).Trim();
                command.Parameters.Add("@Phone", SqlDbType.VarChar, 20).Value = (phone ?? String.Empty).Trim();
                connection.Open();
                object value = command.ExecuteScalar();
                return value == null || value == DBNull.Value ? null : Convert.ToString(value).Trim();
            }
        }

        public static string ToIdentityRole(string staffRole)
        {
            string role = (staffRole ?? String.Empty).Trim();
            if (String.Equals(role, RoleBootstrapper.Manager, StringComparison.OrdinalIgnoreCase)) return RoleBootstrapper.Manager;
            if (String.Equals(role, RoleBootstrapper.Admin, StringComparison.OrdinalIgnoreCase)) return RoleBootstrapper.Admin;
            return RoleBootstrapper.Staff;
        }

        public StaffRecord FindStaffProfile(string email)
        {
            const string sql = @"SELECT TOP (1) staff_First_Name,staff_Surname,staff_email,staff_phone_number,staff_status,staff_role
FROM dbo.Staff
WHERE UPPER(LTRIM(RTRIM(ISNULL(staff_email,''))))=UPPER(@Email);";
            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@Email", SqlDbType.VarChar, 100).Value = (email ?? String.Empty).Trim();
                connection.Open();
                using (var reader = command.ExecuteReader()) return reader.Read() ? ReadStaff(reader) : null;
            }
        }

        public IList<StaffRecord> GetStaffForManager(string managerEmail)
        {
            var result = new List<StaffRecord>();
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var authorization = new SqlCommand(@"SELECT COUNT(*) FROM dbo.Staff
WHERE UPPER(LTRIM(RTRIM(ISNULL(staff_email,''))))=UPPER(@Email)
 AND UPPER(LTRIM(RTRIM(ISNULL(staff_status,''))))='ACTIVE'
 AND UPPER(LTRIM(RTRIM(ISNULL(staff_role,'')))) IN ('MANAGER','ADMIN');", connection))
                {
                    authorization.Parameters.Add("@Email", SqlDbType.VarChar, 100).Value = (managerEmail ?? String.Empty).Trim();
                    if (Convert.ToInt32(authorization.ExecuteScalar()) != 1)
                        throw new InvalidOperationException("An active manager account is required to manage staff.");
                }
                using (var command = new SqlCommand(@"SELECT staff_First_Name,staff_Surname,staff_email,staff_phone_number,staff_status,staff_role
FROM dbo.Staff ORDER BY staff_role,staff_Surname,staff_First_Name;", connection))
                using (var reader = command.ExecuteReader())
                    while (reader.Read()) result.Add(ReadStaff(reader));
            }
            return result;
        }

        public void UpdateStaffForManager(string managerEmail, string targetEmail, string firstName, string surname, string phone, string status)
        {
            ValidateDetails(firstName, surname, phone, status);
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        RequireActiveManager(connection, transaction, managerEmail);
                        string targetRole;
                        int matches;
                        using (var target = new SqlCommand(@"SELECT COUNT(*),MAX(staff_role) FROM dbo.Staff WITH (UPDLOCK,HOLDLOCK)
WHERE UPPER(LTRIM(RTRIM(ISNULL(staff_email,''))))=UPPER(@Email);", connection, transaction))
                        {
                            target.Parameters.Add("@Email", SqlDbType.VarChar, 100).Value = (targetEmail ?? String.Empty).Trim();
                            using (var reader = target.ExecuteReader())
                            {
                                reader.Read(); matches = Convert.ToInt32(reader[0]);
                                targetRole = reader[1] == DBNull.Value ? String.Empty : Convert.ToString(reader[1]).Trim();
                            }
                        }
                        if (matches != 1) throw new InvalidOperationException("That staff record could not be uniquely identified.");
                        if (IsPrivilegedStaffRole(targetRole) && !String.Equals(targetEmail, managerEmail, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException("Managers can edit their own manager record, but not another manager's record.");

                        const string updateSql = @"UPDATE dbo.Staff
SET staff_First_Name=@FirstName,staff_Surname=@Surname,staff_phone_number=@Phone,staff_status=@Status
WHERE UPPER(LTRIM(RTRIM(ISNULL(staff_email,''))))=UPPER(@TargetEmail)
 AND (UPPER(LTRIM(RTRIM(ISNULL(staff_role,'')))) NOT IN ('MANAGER','ADMIN')
      OR UPPER(LTRIM(RTRIM(ISNULL(staff_email,''))))=UPPER(@ManagerEmail));";
                        using (var update = new SqlCommand(updateSql, connection, transaction))
                        {
                            AddStaffParameters(update, firstName, surname, phone, status);
                            update.Parameters.Add("@TargetEmail", SqlDbType.VarChar, 100).Value = targetEmail.Trim();
                            update.Parameters.Add("@ManagerEmail", SqlDbType.VarChar, 100).Value = managerEmail.Trim();
                            if (update.ExecuteNonQuery() != 1) throw new InvalidOperationException("The staff record changed or is not editable. Refresh and try again.");
                        }
                        transaction.Commit();
                    }
                    catch { transaction.Rollback(); throw; }
                }
            }
        }

        public void UpdateOwnManagerProfile(string managerEmail, string firstName, string surname, string phone, string status)
        {
            ValidateDetails(firstName, surname, phone, status);
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        RequireActiveManager(connection, transaction, managerEmail);
                        using (var update = new SqlCommand(@"UPDATE dbo.Staff
SET staff_First_Name=@FirstName,staff_Surname=@Surname,staff_phone_number=@Phone,staff_status=@Status
WHERE UPPER(LTRIM(RTRIM(ISNULL(staff_email,''))))=UPPER(@Email)
 AND UPPER(LTRIM(RTRIM(ISNULL(staff_role,'')))) IN ('MANAGER','ADMIN');", connection, transaction))
                        {
                            AddStaffParameters(update, firstName, surname, phone, status);
                            update.Parameters.Add("@Email", SqlDbType.VarChar, 100).Value = managerEmail.Trim();
                            if (update.ExecuteNonQuery() != 1) throw new InvalidOperationException("Your active manager record could not be uniquely updated.");
                        }
                        transaction.Commit();
                    }
                    catch { transaction.Rollback(); throw; }
                }
            }
        }

        public void UpdateOwnStaffContact(string staffEmail, string firstName, string surname, string phone)
        {
            ValidateDetails(firstName, surname, phone, "Active");
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        using (var authorization = new SqlCommand(@"SELECT COUNT(*) FROM dbo.Staff WITH (UPDLOCK,HOLDLOCK)
WHERE UPPER(LTRIM(RTRIM(ISNULL(staff_email,''))))=UPPER(@Email)
 AND UPPER(LTRIM(RTRIM(ISNULL(staff_status,''))))='ACTIVE'
 AND UPPER(LTRIM(RTRIM(ISNULL(staff_role,'')))) NOT IN ('','GUEST','CUSTOMER','MANAGER','ADMIN');", connection, transaction))
                        {
                            authorization.Parameters.Add("@Email", SqlDbType.VarChar, 100).Value = (staffEmail ?? String.Empty).Trim();
                            if (Convert.ToInt32(authorization.ExecuteScalar()) != 1)
                                throw new InvalidOperationException("An active staff record is required to edit this profile.");
                        }
                        using (var update = new SqlCommand(@"UPDATE dbo.Staff
SET staff_First_Name=@FirstName,staff_Surname=@Surname,staff_phone_number=@Phone
WHERE UPPER(LTRIM(RTRIM(ISNULL(staff_email,''))))=UPPER(@Email)
 AND UPPER(LTRIM(RTRIM(ISNULL(staff_status,''))))='ACTIVE'
 AND UPPER(LTRIM(RTRIM(ISNULL(staff_role,'')))) NOT IN ('','GUEST','CUSTOMER','MANAGER','ADMIN');", connection, transaction))
                        {
                            update.Parameters.Add("@FirstName", SqlDbType.VarChar, 50).Value = firstName.Trim();
                            update.Parameters.Add("@Surname", SqlDbType.VarChar, 50).Value = surname.Trim();
                            update.Parameters.Add("@Phone", SqlDbType.VarChar, 20).Value = phone.Trim();
                            update.Parameters.Add("@Email", SqlDbType.VarChar, 100).Value = staffEmail.Trim();
                            if (update.ExecuteNonQuery() != 1) throw new InvalidOperationException("Your active staff profile could not be uniquely updated.");
                        }
                        transaction.Commit();
                    }
                    catch { transaction.Rollback(); throw; }
                }
            }
        }

        private void RequireActiveManager(SqlConnection connection, SqlTransaction transaction, string email)
        {
            using (var command = new SqlCommand(@"SELECT COUNT(*) FROM dbo.Staff WITH (UPDLOCK,HOLDLOCK)
WHERE UPPER(LTRIM(RTRIM(ISNULL(staff_email,''))))=UPPER(@Email)
 AND UPPER(LTRIM(RTRIM(ISNULL(staff_status,''))))='ACTIVE'
 AND UPPER(LTRIM(RTRIM(ISNULL(staff_role,'')))) IN ('MANAGER','ADMIN');", connection, transaction))
            {
                command.Parameters.Add("@Email", SqlDbType.VarChar, 100).Value = (email ?? String.Empty).Trim();
                if (Convert.ToInt32(command.ExecuteScalar()) != 1)
                    throw new InvalidOperationException("Your active manager access could not be verified.");
            }
        }

        private static void ValidateDetails(string firstName, string surname, string phone, string status)
        {
            if (String.IsNullOrWhiteSpace(firstName) || firstName.Trim().Length > 50)
                throw new InvalidOperationException("Enter a first name up to 50 characters.");
            if (ContainsControlCharacter(firstName) || ContainsControlCharacter(surname))
                throw new InvalidOperationException("Names cannot contain control characters.");
            if (String.IsNullOrWhiteSpace(surname) || surname.Trim().Length > 50)
                throw new InvalidOperationException("Enter a surname up to 50 characters.");
            if (String.IsNullOrWhiteSpace(phone) || phone.Trim().Length > 20)
                throw new InvalidOperationException("Enter a phone number up to 20 characters.");
            if (!Regex.IsMatch(phone.Trim(), @"^[0-9+() .-]{7,20}$"))
                throw new InvalidOperationException("Enter a valid phone number using digits, spaces, +, parentheses, hyphens, or periods.");
            if (!String.Equals(status, "Active", StringComparison.OrdinalIgnoreCase) &&
                !String.Equals(status, "Inactive", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Choose Active or Inactive.");
        }

        private static void AddStaffParameters(SqlCommand command, string firstName, string surname, string phone, string status)
        {
            command.Parameters.Add("@FirstName", SqlDbType.VarChar, 50).Value = firstName.Trim();
            command.Parameters.Add("@Surname", SqlDbType.VarChar, 50).Value = surname.Trim();
            command.Parameters.Add("@Phone", SqlDbType.VarChar, 20).Value = phone.Trim();
            command.Parameters.Add("@Status", SqlDbType.VarChar, 30).Value = String.Equals(status, "Active", StringComparison.OrdinalIgnoreCase) ? "Active" : "Inactive";
        }

        private static bool IsPrivilegedStaffRole(string role)
        { return String.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase) || String.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase); }

        private static bool ContainsControlCharacter(string value)
        {
            if (value == null) return false;
            foreach (char character in value) if (Char.IsControl(character)) return true;
            return false;
        }

        private static StaffRecord ReadStaff(SqlDataReader reader)
        {
            return new StaffRecord
            {
                FirstName = Convert.ToString(reader["staff_First_Name"]),
                Surname = Convert.ToString(reader["staff_Surname"]),
                Email = Convert.ToString(reader["staff_email"]),
                Phone = Convert.ToString(reader["staff_phone_number"]),
                Status = Convert.ToString(reader["staff_status"]),
                Role = Convert.ToString(reader["staff_role"])
            };
        }
    }
}
