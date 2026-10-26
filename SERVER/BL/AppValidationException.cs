namespace RMS.BL
{
    public class AppValidationException : Exception
    {
        public AppValidationException(string message) : base(message)
        {
        }
    }
}
