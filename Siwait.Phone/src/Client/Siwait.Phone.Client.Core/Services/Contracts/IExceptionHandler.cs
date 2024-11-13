﻿using System.Runtime.CompilerServices;

namespace Siwait.Phone.Client.Core.Services.Contracts;

public interface IExceptionHandler
{
    void Handle(Exception exception, 
        Dictionary<string, object?>? parameters = null,
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "");
}
