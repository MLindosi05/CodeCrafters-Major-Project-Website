/* Run this once in the GroupPmb2 database after creating the relevant ASP.NET Identity users.
   Change the email addresses below before running. Never put staff passwords in this script. */
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @Roles TABLE (Name nvarchar(256));
INSERT INTO @Roles (Name) VALUES ('Guest'), ('Staff'), ('Manager'), ('Admin');

INSERT INTO AspNetRoles (Id, Name)
SELECT CONVERT(nvarchar(128), NEWID()), r.Name
FROM @Roles r
WHERE NOT EXISTS (SELECT 1 FROM AspNetRoles ar WHERE ar.Name = r.Name);

DECLARE @Assignments TABLE (Email nvarchar(256), RoleName nvarchar(256));
INSERT INTO @Assignments (Email, RoleName) VALUES
    ('replace-manager@regalinn.co.za', 'Manager'),
    ('replace-admin@regalinn.co.za', 'Admin');

INSERT INTO AspNetUserRoles (UserId, RoleId)
SELECT u.Id, r.Id
FROM @Assignments a
JOIN AspNetUsers u ON u.Email = a.Email
JOIN AspNetRoles r ON r.Name = a.RoleName
WHERE NOT EXISTS (
    SELECT 1 FROM AspNetUserRoles ur WHERE ur.UserId = u.Id AND ur.RoleId = r.Id
);

COMMIT TRANSACTION;
