CREATE COLUMN TABLE Tenants (
    Id NVARCHAR(36) NOT NULL,
    TenantId NVARCHAR(36) NOT NULL,
    Name NVARCHAR(200) NOT NULL,
    Slug NVARCHAR(64) NOT NULL,
    CreatedAt TIMESTAMP NOT NULL,
    PRIMARY KEY (Id)
);

CREATE UNIQUE INDEX UX_Tenants_TenantId_Id ON Tenants (TenantId, Id);
CREATE UNIQUE INDEX UX_Tenants_Slug ON Tenants (Slug);

CREATE COLUMN TABLE TenantUsers (
    Id NVARCHAR(36) NOT NULL,
    TenantId NVARCHAR(36) NOT NULL,
    ExternalSubject NVARCHAR(256) NOT NULL,
    DisplayName NVARCHAR(200) NOT NULL,
    CreatedAt TIMESTAMP NOT NULL,
    PRIMARY KEY (Id)
);

CREATE UNIQUE INDEX UX_TenantUsers_TenantId_Id ON TenantUsers (TenantId, Id);
CREATE UNIQUE INDEX UX_TenantUsers_TenantId_Subject ON TenantUsers (TenantId, ExternalSubject);

CREATE COLUMN TABLE TenantUserRoles (
    Id NVARCHAR(36) NOT NULL,
    TenantId NVARCHAR(36) NOT NULL,
    UserId NVARCHAR(36) NOT NULL,
    RoleName NVARCHAR(64) NOT NULL,
    PRIMARY KEY (Id)
);

CREATE UNIQUE INDEX UX_TenantUserRoles_TenantId_Id ON TenantUserRoles (TenantId, Id);
CREATE UNIQUE INDEX UX_TenantUserRoles_User_Role ON TenantUserRoles (TenantId, UserId, RoleName);
