SELECT PackageRegistrations.Id 'PackageId', Packages.Version 'PackageVersion', Packages.[Created]
FROM Packages
INNER JOIN PackageRegistrations ON PackageRegistrations.[Key] = Packages.[PackageRegistrationKey]
WHERE Packages.[Created] >= CONVERT(DATE, DATEADD(day, -3, GETDATE()))
  And Packages.[IsLatestStable] = 1

