namespace LethalDrink.Core
{
    public readonly struct ActionResult
    {
        public bool Success
        {
            get;
        }
        public ErrorCode Error
        {
            get;
        }
        public string Message
        {
            get;
        }
        private ActionResult(bool success, ErrorCode error, string message)
        {
            Success = success;
            Error = error;
            Message = message;
        }
        public static ActionResult Ok(string message = "") => new ActionResult(true, ErrorCode.None, message);
        public static ActionResult Fail(ErrorCode error, string message) => new ActionResult(false, error, message);
        public override string ToString() => Success ? "OK " + Message : Error + ": " + Message;
    }
}
