using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Xsl;
using CommandLine;
using CommandLine.Text;

namespace JaySvcUtil
{

    public class Program
    {
        public sealed class Options : CommandLineOptionsBase
        {
            [Option("m", "metadataUri", Required = true, HelpText = "The uri of the oData $metadata definition. Can be an online resource or a local file as well")]
            public string MetadataUri;

            [Option("o", "out", HelpText = "The name of the generated output file. Default is JayDataContext.js")]
            public string OutputFileName = "JayDataContext.js";

            [Option("n", "namespace", HelpText = "The namespace of the generated JayData EntitContext class. Default is taken from the metadata.")]
            public string ContextNamespace = "";

            [Option("c", "contextBaseClass", HelpText = "The name of the base class for the generated entity context. Default is $data.EntityContext")]
            public string ContextBaseClass = "$data.EntityContext";

            [Option("e", "entityBaseClass", HelpText = "The name of the base class for the generated entity types. Default is $data.Entity.")]
            public string EntityBaseClass = "$data.Entity";

            [Option("s", "entitySetBaseClass", HelpText = "The name of the base class for the generated entity sets. Default is $data.EntitySet.")]
            public string EntitySetBaseClass = "$data.EntitySet";

            [Option("a", "collectionBaseClass", HelpText = "The name of the base class for the generated entity sets. Default is Array.")]
            public string CollectionBaseClass = "Array";

            [Option("b", "autoCreateContext", HelpText = "Create an instance of the context with default parameters. Default is true")]
            public bool AutoCreateContext = true;

            [Option("x", "contextInstanceName", HelpText = "The name of the automatically generated context instance under the context namespace. Default is 'context'")]
            public string ContextInstanceName = "context";

            [HelpOption(HelpText = "Dispaly this help screen.")]
            public string GetUsage()
            {
                var help = new HelpText("JaySvcUtil 1.0.4", "open source");
                help.AdditionalNewLineAfterOption = true;
                help.Copyright = new CopyrightInfo("JayData project", 2012);
                this.HandleParsingErrorsInHelp(help);
                help.AddOptions(this);
                return help;
            }

            [Option("u", "userName", HelpText = "The network username for an authenticated OData service")]
            public string UserName = string.Empty;

            [Option("p", "password", HelpText = "The network password for an authenticated OData service")]
            public string Password = string.Empty;
            
            [Option("d", "domain", HelpText = "The network domain for an authenticated OData service")]
            public string Domain = string.Empty;

            [Option("v", "protocolVersion", HelpText="The OData version of the service: V1, V2 or V3. Autodetect if missing")]
            public string ODataVersion = "";

            private void HandleParsingErrorsInHelp(HelpText help)
            {
                string errors = help.RenderParsingErrorsText(this);
                if (!string.IsNullOrEmpty(errors))
                {
                    help.AddPreOptionsLine(string.Concat(Environment.NewLine, "ERROR: ", errors, Environment.NewLine));
                }
            }

            //XSLT helper functions
            public string GetContextNamesace(string actualNamespace)
            {
                return (ContextNamespace == string.Empty) ? actualNamespace : ContextNamespace;
            }

            public string GetContextBaseClass()
            {
                return ContextBaseClass;
            }

            public string GetEntityBaseClass()
            {
                return EntityBaseClass;
            }

            public string GetCollectionBaseClass()
            {
                return CollectionBaseClass;

            }

            public string GetEntitySetBaseClass()
            {
                return EntitySetBaseClass;
            }

            public string GetSerivceUri()
            {
                return MetadataUri.Trim().Replace("/$metadata", "");
            }

            public bool  GetAutoCreateContext() { return AutoCreateContext; }

            public string GetContextInstanceName() { return ContextInstanceName; }
        }

        /*
         * 
         /serviceUri default: - 
         /output default: entitycontext.js
         /contextNamespace default: -
         /debug default: false
         /entityBaseClass default: $data.Entity
         /contextBaseClass default: $data.EntityContext
         /collectionBaseClass default: Array
         /entitySetBaseClass default: $data.EntitySet
        */
        private const string stylesheet = @"JayDataContextGenerator.xslt";



        static IXPathNavigable GetXslt(string version)
        {
            var filename = "JayDataContextGenerator_OData_" + version + ".xslt";
            if (File.Exists(filename)) {
                return new XPathDocument(File.OpenText(filename));
            } else {
                var _asm = Assembly.GetExecutingAssembly();
                using (var str = _asm.GetManifestResourceStream("JaySvcUtil." + filename))
                {
                    return new XPathDocument(str);
                }
            }
        }

        //http://schemas.microsoft.com/ado/2007/05/edm <-- i found this somewhere --> sharepoint
        public static Dictionary<string, string> NamespaceVersions  = new Dictionary<string,string>
        {
            {"http://schemas.microsoft.com/ado/2007/05/edm", "V11" },
            {"http://schemas.microsoft.com/ado/2006/04/edm", "V1" },
            {"http://schemas.microsoft.com/ado/2008/09/edm", "V2" },
            {"http://schemas.microsoft.com/ado/2009/08/edm", "V21" },
            {"http://schemas.microsoft.com/ado/2009/11/edm", "V3" }
        };

        static void Main(string[] args)
        {
            var options = new Options();
            ICommandLineParser parser = new CommandLineParser(new CommandLineParserSettings(Console.Error));
            if (!parser.ParseArguments(args, options))
                Environment.Exit(1);


            
            Console.Write("Requesting: " + options.MetadataUri + "...");

            MemoryStream documentStream = new MemoryStream();
            if (options.MetadataUri.StartsWith("http"))
            {
                var r = new System.Net.WebClient();
                var req = (HttpWebRequest)HttpWebRequest.Create(options.MetadataUri.Trim());
                req.UserAgent = "JaySvcUtil.exe";

                //req.Credentials = cred;
                req.PreAuthenticate = true;
                //req.Headers.Add("User-Agent: JaySvcUtil.exe");
                if (!string.IsNullOrWhiteSpace(options.UserName))
                {
                    req.Credentials = new NetworkCredential(options.UserName, options.Password, options.Domain);
                }
                else
                {
                    req.Credentials = CredentialCache.DefaultCredentials;
                }
                var res = req.GetResponse();
                var resStream = res.GetResponseStream();
                resStream.CopyTo(documentStream);
                documentStream.Position = 0;
                //resStream.Position = 0;
                //r.Credentials = cc;
            }
            else
            {
                File.OpenRead(options.MetadataUri).CopyTo(documentStream);
                documentStream.Position = 0;
            }
            //var metadata = r.DownloadString(options.MetadataUri.Trim());
            Console.WriteLine(" done.");
          //// Compile the style sheet.
          //// Execute the XSLT transform.
            FileStream outputStream = new FileStream(options.OutputFileName, FileMode.Create);
            //MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(metadata));
            XmlDocument doc = new XmlDocument();
            doc.Load(documentStream);
            documentStream.Position = 0;
            var dsNode = doc.SelectSingleNode("//*[local-name() = 'DataServices']");
            var schemaNode = doc.SelectSingleNode("//*[local-name() = 'Schema']");

            var ns = schemaNode.NamespaceURI;

            if (string.IsNullOrWhiteSpace(options.ODataVersion))
            {
                options.ODataVersion = NamespaceVersions[ns];
            }
           

            XslCompiledTransform xslt = new XslCompiledTransform(Debugger.IsAttached);
            if (Debugger.IsAttached)
            {
                xslt.Load("JayDataContextGenerator_odata_" + options.ODataVersion + ".xslt");
            }
            else
            {
                xslt.Load(GetXslt(options.ODataVersion));
            }

            Console.WriteLine("OData version: " + options.ODataVersion);
            var xslArg = new XsltArgumentList();
            xslArg.AddExtensionObject("jay:stack",options);
            var reader = XmlReader.Create(documentStream);
            xslt.Transform(reader, xslArg, outputStream);
        }

        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            
            Console.WriteLine("Error:" + ((Exception)e.ExceptionObject).Message);
       
        }
    }
}
