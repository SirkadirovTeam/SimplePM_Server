﻿using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using IniParser;
using IniParser.Model;
using System.IO;
using System.Web;
using System.Web.UI;

namespace SimplePM_Server
{
    class SimplePM_Compiler
    {
        private ulong submissionId;
        private string fileLocation, fileExt;
        private IniData sConfig;

        public SimplePM_Compiler(IniData sConfig, ulong submissionId, string fileExt)
        {
            if (string.IsNullOrEmpty(fileExt) || string.IsNullOrWhiteSpace(fileExt))
                throw new ArgumentNullException("fileExt", "File extension error!");

            fileLocation = sConfig["Program"]["tempPath"] + submissionId.ToString() + fileExt;

            if (submissionId <= 0)
                throw new ArgumentNullException("submissionId", "Submission ID invalid!");
            if (string.IsNullOrEmpty(fileLocation) || string.IsNullOrWhiteSpace(fileLocation) || !File.Exists(fileLocation))
                throw new ArgumentNullException("fileLocation", "File not found!");

            this.sConfig = sConfig;
            this.submissionId = submissionId;
        }

        public class CompilerResult
        {
            public bool hasErrors = false;
            public string exe_fullname = null;
            public string compilerMessage = null;
        }

        public CompilerResult startFreepascalCompiler()
        {
            Process fpcProc = new Process();

            ProcessStartInfo pStartInfo = new ProcessStartInfo(sConfig["Compilers"]["freepascal_location"], fileLocation + " -ve -vw -vn -vh -vi -vl -vs -vb -vd -vq");
            pStartInfo.ErrorDialog = false;
            pStartInfo.WindowStyle = ProcessWindowStyle.Minimized;
            pStartInfo.RedirectStandardOutput = true;
            pStartInfo.UseShellExecute = false;

            fpcProc.StartInfo = pStartInfo;
            fpcProc.Start();
            
            StreamReader reader = fpcProc.StandardOutput;

            CompilerResult result = new CompilerResult();
            result.compilerMessage = HttpUtility.HtmlEncode(reader.ReadToEnd());
            
            fpcProc.WaitForExit();
            
            return returnCompilerResult(result);
        }

        private CompilerResult returnCompilerResult(CompilerResult temporaryResult)
        {
            string exeLocation = sConfig["Program"]["tempPath"] + submissionId.ToString() + ".exe";
            temporaryResult.exe_fullname = exeLocation;

            if (File.Exists(exeLocation))
                temporaryResult.hasErrors = false;
            else
                temporaryResult.hasErrors = true;

            return temporaryResult;
        }
    }
}
