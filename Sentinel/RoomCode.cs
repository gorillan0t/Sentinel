namespace Sentinel;

public static class RoomCode
{
    public static string Add(string code, char key)
    {
        code = code ?? "";
        if (code.Length < 10 && char.IsLetterOrDigit(key))
        {
            return code + char.ToUpperInvariant(key);
        }

        return code;
    }

    public static string Backspace(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return "";
        }

        return code.Substring(0, code.Length - 1);
    }

    public static bool Valid(string code)
    {
        if (!string.IsNullOrEmpty(code) && code.Length <= 10)
        {
            for (int i = 0; i < code.Length; i++)
            {
                if (!char.IsLetterOrDigit(code[i]))
                {
                    return false;
                }
            }

            return true;
        }

        return false;
    }
}