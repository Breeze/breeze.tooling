Important:

The Microsoft.EntityFrameworkCore and Microsoft.EntityFrameworkCore.SqlServer nuget packages  
are loaded in this project NOT because they are used by this app directly but because these 
Assemblies are typically referenced via nuget by the EFContext dll that is app will reflect over
and for which metadata will be generated.  

Unfortanately, in .NET Core, these dependent assemblies will not be found when loading the 
target EFContext dll assembly ... hence this hack.
