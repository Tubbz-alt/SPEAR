using SPEAR.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPEAR.Parsers
{
    public abstract class FileParser
    {
        /////////////////////////////////////////////////////////////////////////////////////////
        // Properties
        /////////////////////////////////////////////////////////////////////////////////////////
        private IFileParserCallback callback;

        bool HaveErrorsOccurred { get; }
        List<KeyValuePair<string, string>> FileErrors { get; }

        public abstract string FileName { get; }

        /////////////////////////////////////////////////////////////////////////////////////////
        // Required Methods
        /////////////////////////////////////////////////////////////////////////////////////////
        public abstract IEnumerable<string> GetAllFilePaths(string directoryPath);
        public abstract void InitializeFilePaths(IEnumerable<string> allFilePaths);
        public abstract void Parse();
        public abstract void Cleanup();


        /////////////////////////////////////////////////////////////////////////////////////////
        // Methods
        /////////////////////////////////////////////////////////////////////////////////////////
        public void RegisterCallback(IFileParserCallback callback)
        {
            this.callback = callback;
        }

        protected void Invoke_ParsingStarted()
        {
            if (callback != null)
                callback.ParsingStarted();
        }

        protected void Invoke_ParsingUpdate(float percentComplete)
        {
            if (callback != null)
                callback.ParsingUpdate(percentComplete);
        }

        protected void Invoke_ParsingComplete(IEnumerable<DeviceData> deviceDatas)
        {
            if (callback != null)
                callback.ParsingComplete(deviceDatas);
        }

        protected void Invoke_ParsingError(string title, string message)
        {
            if (callback != null)
                callback.ParsingError(title, message);
        }
    }
}
