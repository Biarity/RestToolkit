namespace RestToolkit.Base
{
    public interface IAdditionalUserInfo<TUser>
        where TUser : ToolkitUser
    {
        void Map(ref TUser user);
    }
}
