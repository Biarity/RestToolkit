# RestToolkit
ASP.NET Core REST Toolkit - for quickly creating clean REST APIs in ASP.NET Core 

# Example `AdditionalUserInfo` code

```C#
public class ExampleAccountController 
	: AccountController<IExampleAdditionalUserInfo, ExampleUser, ExampleAdditionalUserInfo>
{
	//ctor...
}

public interface IExampleAdditionalUserInfo : IAdditionalUserInfo<ExampleUser>
{
    string UserName { get; set; }
}

[DataContract]
public class ExampleUser : ToolkitUser, IExampleAdditionalUserInfo
{
    public void Map(ref ExampleUser user)
    {
        throw new System.NotImplementedException();
    }
}

public class ExampleAdditionalUserInfo : IExampleAdditionalUserInfo
{
    [Required]
    [MinLength(3), MaxLength(10)]
    [RegularExpression("[A-Za-z0-9]+")]
    [PersonalData]
    public string UserName { get; set; }

    public void Map(ref ExampleUser user)
    {
        user.UserName = UserName;
    }
}
```