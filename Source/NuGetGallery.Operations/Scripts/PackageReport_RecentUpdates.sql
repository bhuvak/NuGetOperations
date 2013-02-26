SELECT TOP(1) 
Packages.[Version] 'PackageVersion',
DATEDIFF(day, Packages.[Created], GETDATE()) 'Days'
FROM Packages
INNER JOIN PackageRegistrations ON Packages.[PackageRegistrationKey] = PackageRegistrations.[Key]
WHERE PackageRegistrations.[Id] = @PackageId
ORDER BY Packages.[Created] DESC