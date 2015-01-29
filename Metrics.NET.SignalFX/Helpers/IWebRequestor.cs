﻿
using System.IO;

namespace Metrics.SignalFX.Helpers
{
    public interface IWebRequestor
    {
        Stream GetWriteStream();

        Stream Send();
    }
}