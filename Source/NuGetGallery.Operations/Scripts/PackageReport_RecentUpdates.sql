SELECT TOP(1) 
Packages.[Version] 'PackageVersion',
DATEDIFF(day, Packages.[Created], GETDATE()) 'Days'
FROM Packages
INNER JOIN PackageRegistrations ON Packages.[PackageRegistrationKey] = PackageRegistrations.[Key]
WHERE PackageRegistrations.[Id] = @PackageId
  AND DATEDIFF(day, Packages.[Created], GETDATE()) <= 14
  AND Packages.[Listed] = 1
ORDER BY Packages.[Created] DESC