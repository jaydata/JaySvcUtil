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

            [Option("b", "autoCreateContext", HelpText = "Create an instance of the context with default parameters. Default is false")]
            public bool AutoCreateContext = false;

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

            [Option("d", "maxDataServiceVersion", HelpText = "The OData MaxDataServiceVersion of the service: 1.0, 2.0 or 3.0. Autodetect if missing")]
            public string ODataMaxDataServiceVersion = "";

            [Option("f", "typeFilter", HelpText = "Model type filter")]
            public string TypeFilter = string.Empty;

            [Option("t", "typeFilterConfig", HelpText = "Model type filter from config XML")]
            public string TypeFilterConfig = string.Empty;

            [Option("D", "dependentRelationsOnly", HelpText = "Model with dependent relations only")]
            public bool DependentRelationsOnly = false;

            [Option("N", "navigation", HelpText = "Model with navigation properties (default: true)")]
            public string Navigation = "true";

            [Option("K", "generateKeys", HelpText = "Generate key fields of types when using typeFilter with explicit member projection (default: true)")]
            public string GenerateKeys = "true";

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
        private const string stylesheetTS = @"JayDataContextTypeScriptGenerator.xslt";
        private const string stylesheetConfig = @"TypeFilterConfig.xslt";


        /*static IXPathNavigable GetXslt(string version) { 
            return GetXslt(version, false);
        }*/

        static IXPathNavigable GetXslt(string metaNamespace, bool typeScript)
        {
            var filename = typeScript != true ? stylesheet : stylesheetTS;

            var _asm = Assembly.GetExecutingAssembly();
            using (Stream str = _asm.GetManifestResourceStream("JaySvcUtil." + filename))
            using (var sr = new StreamReader(str))
            {
                string master = sr.ReadToEnd();

                master = master.Replace("xmlns:edm=\"@@VERSIONNS@@\"", "xmlns:edm=\"" + metaNamespace + "\"");
                if(NamespaceVersions.Keys.Contains(metaNamespace))
                    master = master.Replace("@@VERSION@@", NamespaceVersions[metaNamespace]);
                else
                    master = master.Replace("@@VERSION@@", "Unknown");

                return new XPathDocument(new StringReader(master));
            }
            
            //if (File.Exists(filename))
            //{
            //    return new XPathDocument(File.OpenText(filename));
            //}
            //else
            //{
            //    //var _asm = Assembly.GetExecutingAssembly();
            //    using (var str = _asm.GetManifestResourceStream("JaySvcUtil." + filename))
            //    {
            //        return new XPathDocument(str);
            //    }
            //}
        }

        static void buildDebugXslt(string metaNamespace, bool typeScript)
        {
            var filename = typeScript != true ? stylesheet : stylesheetTS;
            var latestXslt = String.Empty;
            
            if (File.Exists(filename)) {
                using (var sr = File.OpenText(filename)) {
                    latestXslt = sr.ReadToEnd();
                    //string master = sr.ReadToEnd();
                    //if (master.Contains("xmlns:edm=\"" + metaNamespace + "\"")) {
                    //    return;
                    //}
                }
            }


            var _asm = Assembly.GetExecutingAssembly();
            using (Stream str = _asm.GetManifestResourceStream("JaySvcUtil." + filename))
            using (var sr = new StreamReader(str))
            {
                string master = sr.ReadToEnd();

                master = master.Replace("xmlns:edm=\"@@VERSIONNS@@\"", "xmlns:edm=\"" + metaNamespace + "\"");
                if (NamespaceVersions.Keys.Contains(metaNamespace))
                    master = master.Replace("@@VERSION@@", NamespaceVersions[metaNamespace]);
                else
                    master = master.Replace("@@VERSION@@", "Unknown");

                if(latestXslt != master)
                    File.WriteAllText(filename, master);
            }
        }

        //http://schemas.microsoft.com/ado/2007/05/edm <-- i found this somewhere --> sharepoint
        public static Dictionary<string, string> NamespaceVersions  = new Dictionary<string,string>
        {
            {"http://schemas.microsoft.com/ado/2007/05/edm", "V1.1" },
            {"http://schemas.microsoft.com/ado/2006/04/edm", " V1 " },
            {"http://schemas.microsoft.com/ado/2008/09/edm", " V2 " },
            {"http://schemas.microsoft.com/ado/2009/08/edm", "V2.1" },
            {"http://schemas.microsoft.com/ado/2009/11/edm", " V3 " }
        };

        public static Dictionary<string, string> maxNamespaceVersions = new Dictionary<string, string>
        {
            {"http://schemas.microsoft.com/ado/2007/05/edm", "1.0" },
            {"http://schemas.microsoft.com/ado/2006/04/edm", "1.0" },
            {"http://schemas.microsoft.com/ado/2008/09/edm", "2.0" },
            {"http://schemas.microsoft.com/ado/2009/08/edm", "2.0" },
            {"http://schemas.microsoft.com/ado/2009/11/edm", "3.0" }
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
            var maxDSVersion = dsNode.Attributes["m:MaxDataServiceVersion"];
            var version = maxDSVersion != null ? maxDSVersion.Value : "";

            string metadataTypes = options.TypeFilter;

            if (metadataTypes == string.Empty && options.TypeFilterConfig != string.Empty) {
                MemoryStream configStream = new MemoryStream();
                File.OpenRead(options.TypeFilterConfig).CopyTo(configStream);
                configStream.Position = 0;

                XmlDocument configDoc = new XmlDocument();
                configDoc.Load(configStream);
                configStream.Position = 0;

                XslCompiledTransform configXslt = new XslCompiledTransform(Debugger.IsAttached);
                var _asm = Assembly.GetExecutingAssembly();
                using (Stream str = _asm.GetManifestResourceStream("JaySvcUtil." + stylesheetConfig))
                using (var sr = new StreamReader(str))
                {
                    configXslt.Load(new XPathDocument(sr));
                }
                var configReader = XmlReader.Create(configStream);

                StringWriter sw = new StringWriter();
                XmlWriter xwo = XmlWriter.Create(sw, configXslt.OutputSettings);
                configXslt.Transform(configReader, null, xwo);

                metadataTypes = sw.ToString();
            }

            if (metadataTypes != string.Empty)
            {
                XmlNamespaceManager nsmanager = new XmlNamespaceManager(doc.NameTable);
                nsmanager.AddNamespace("edmx", ns);

                List<string> datas = metadataTypes.Split(new char[]{';'}, StringSplitOptions.RemoveEmptyEntries).ToList();
                List<TypeData> types = new List<TypeData>();
                for (int i = 0; i < datas.Count; i++)
                {
                    if (datas[i] != String.Empty) {
                        TypeData typeData = new TypeData(datas[i]);
                        string typeShortName = typeData.Name;
                        string containerName = string.Empty;
                        if (typeData.Name.LastIndexOf('.') > 0)
                        {
                            containerName = typeData.Name.Substring(0, typeData.Name.LastIndexOf('.'));
                            typeShortName = typeData.Name.Substring(typeData.Name.LastIndexOf('.') + 1);
                        }

                        var conainers = doc.SelectNodes("//edmx:EntityContainer[@Name = '" + containerName + "']", nsmanager);
                        for (int j = 0; j < conainers.Count; j++)
                        {
                            var entitySetDef = conainers[j].SelectSingleNode("edmx:EntitySet[@Name = '" + typeShortName + "']", nsmanager);
                            if (entitySetDef != null)
                            {
                                typeData.Name = entitySetDef.Attributes["EntityType"].Value;
                                break;
                            }

                        }
                        types.Add(typeData);
                        
                    }
                }

                List<TypeData> discoveredData = null;
                if (options.DependentRelationsOnly)
                {
                    discoveredData = DiscoverProperyDependencies(types, doc, nsmanager, options.Navigation == "true", options.GenerateKeys == "true");
                }
                else
                {
                    discoveredData = DiscoverTypeDependencies(types, doc, nsmanager, options.Navigation == "true", options.GenerateKeys == "true");
                }

                var complex = doc.SelectNodes("//*[local-name() = 'ComplexType']");
                for (int i = 0; i < complex.Count; i++)
                {
                    string cns = complex[i].SelectSingleNode("../.").Attributes["Namespace"].Value;
                    string data = cns == string.Empty ? complex[i].Attributes["Name"].Value : (cns + "." + complex[i].Attributes["Name"].Value);
                    discoveredData.Add(new TypeData(data));
                }

                string result = "";
                for (int i = 0; i < discoveredData.Count; i++)
                {
                    TypeData row = discoveredData[i];
                    if (row.Fields.Count > 0) {
                        result += row.Name + ":" + string.Join(",", row.Fields) + ";";
                    }
                    else {
                        result += row.Name + ";";
                    }
                }

                metadataTypes = result;
            }

            //if (string.IsNullOrWhiteSpace(options.ODataVersion))
            //{
                options.ODataVersion = NamespaceVersions.Keys.Contains(ns) ? NamespaceVersions[ns] : "Unknown";

                if (version != String.Empty)
                    options.ODataMaxDataServiceVersion = version;
                else
                    options.ODataMaxDataServiceVersion = maxNamespaceVersions.Keys.Contains(ns) ? maxNamespaceVersions[ns] : "3.0";
            //}
           

            XslCompiledTransform xslt = new XslCompiledTransform(Debugger.IsAttached);
            if (Debugger.IsAttached)
            {
                buildDebugXslt(ns, false);
                xslt.Load(stylesheet);
            }
            else
            {
                xslt.Load(GetXslt(ns, false));
            }

            Console.WriteLine("OData version: " + options.ODataVersion);
            var xslArg = new XsltArgumentList();

            int metaIdx = options.MetadataUri.LastIndexOf("$metadata");
            if (metaIdx > 0) {
                xslArg.AddParam("SerivceUri", "", options.MetadataUri.Substring(0, options.MetadataUri.LastIndexOf("$metadata") - 1));
            }
            else {
                xslArg.AddParam("SerivceUri", "", "");
                options.AutoCreateContext = false;
            }
            xslArg.AddParam("EntityBaseClass", "", options.EntityBaseClass);
            xslArg.AddParam("ContextBaseClass", "", options.ContextBaseClass);
            xslArg.AddParam("AutoCreateContext", "", options.AutoCreateContext);
            xslArg.AddParam("ContextInstanceName", "", options.ContextInstanceName);
            xslArg.AddParam("EntitySetBaseClass", "", options.EntitySetBaseClass);
            xslArg.AddParam("CollectionBaseClass", "", options.CollectionBaseClass);
            xslArg.AddParam("DefaultNamespace", "", "");
            xslArg.AddParam("contextNamespace", "", options.ContextNamespace);
            xslArg.AddParam("MaxDataserviceVersion", "", options.ODataMaxDataServiceVersion);
            xslArg.AddParam("AllowedTypesList", "", metadataTypes);
            xslArg.AddParam("GenerateNavigationProperties", "", options.Navigation == "true");

            var reader = XmlReader.Create(documentStream);
            xslt.Transform(reader, xslArg, outputStream);

            return;

            Console.WriteLine("Generating TypeScript document");

            XslCompiledTransform xsltTS = new XslCompiledTransform(Debugger.IsAttached);
            documentStream.Position = 0;

            if (Debugger.IsAttached)
            {
                buildDebugXslt(ns, true);
                xsltTS.Load(stylesheetTS);
            }
            else
            {
                xsltTS.Load(GetXslt(ns, true));
            }

            FileStream outputStreamTS = new FileStream(options.OutputFileName.Substring(0, options.OutputFileName.Length - 3) + ".d.ts", FileMode.Create);
            reader = XmlReader.Create(documentStream);
            xsltTS.Transform(reader, xslArg, outputStreamTS);
        }

        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            
            Console.WriteLine("Error:" + ((Exception)e.ExceptionObject).Message);
       
        }

        static List<TypeData> DiscoverTypeDependencies(List<TypeData> types, XmlDocument doc, XmlNamespaceManager nsmanager, bool withNavPropertis, bool withKeys)
        {
            List<TypeData> allowedTypes = new List<TypeData>();
            List<string> allowedTypeNames = new List<string>();
            List<string> collect = new List<string>();

            for (int i = 0; i < types.Count; i++)
            {
                collect.Remove(types[i].Name);
                discoverType(types[i], doc, nsmanager, allowedTypes, allowedTypeNames, withNavPropertis, withKeys, true, collect);
            }

            for (int i = 0; i < collect.Count; i++)
            {
                discoverType(new TypeData(collect[i]), doc, nsmanager, allowedTypes, allowedTypeNames, withNavPropertis, withKeys, false, new List<string>());
            }

            return allowedTypes;
        }
        static void discoverType(TypeData typeData, XmlDocument doc, XmlNamespaceManager nsmanager, List<TypeData> allowedTypes, List<string> allowedTypeNames, bool withNavPropertis, bool withKeys, bool collectTypes, List<string> collectedTypes)
        {
            string typeName = typeData.Name;

            if (allowedTypeNames.Contains(typeName))
            {
                return;
            }
            Console.WriteLine("Discover: " + typeName);

            string typeShortName = typeName;
            string typeNamespace = string.Empty;
            if (typeName.LastIndexOf('.') > 0)
            {
                typeNamespace = typeName.Substring(0, typeName.LastIndexOf('.'));
                typeShortName = typeName.Substring(typeName.LastIndexOf('.') + 1);
            }

            var schemaNode = doc.SelectSingleNode("//edmx:Schema[@Namespace = '" + typeNamespace + "']", nsmanager);
            if (schemaNode != null)
            {
                var typeNode = schemaNode.SelectSingleNode("edmx:EntityType[@Name = '" + typeShortName + "'] | edmx:ComplexType[@Name = '" + typeShortName + "']", nsmanager);
                if (typeNode != null)
                {
                    allowedTypes.Add(typeData);
                    allowedTypeNames.Add(typeName);

                    if (withKeys && typeData.Fields.Count > 0) {
                        var keys = typeNode.SelectNodes("edmx:Key/edmx:PropertyRef", nsmanager);
                        if (keys != null)
                        {
                            for (int j = 0; j < keys.Count; j++)
                            {
                                string keyField = keys[j].Attributes["Name"].Value;
                                if (!typeData.Fields.Contains(keyField))
                                    typeData.Fields.Insert(j, keyField);
                            }
                        }
                    }

                    if (withNavPropertis)
                    {
                        var navPropNodes = typeNode.SelectNodes("edmx:NavigationProperty", nsmanager);
                        for (int j = 0; j < navPropNodes.Count; j++)
                        {
                            var navProp = navPropNodes[j];
                            if (typeData.Fields.Count == 0 || typeData.Fields.Contains(navProp.Attributes["Name"].Value))
                            {

                                var FromRole = navProp.Attributes["FromRole"].Value;
                                var ToRole = navProp.Attributes["ToRole"].Value;

                                var association = schemaNode.SelectSingleNode("edmx:Association/edmx:End[@Role = '" + FromRole + "' and @Type != '" + typeName + "']", nsmanager);
                                if (association == null)
                                {
                                    association = schemaNode.SelectSingleNode("edmx:Association/edmx:End[@Role = '" + ToRole + "' and @Type != '" + typeName + "']", nsmanager);
                                }

                                if (association != null)
                                {
                                    string nav_type = association.Attributes["Type"].Value;

                                    if (collectTypes)
                                    {
                                        if (!collectedTypes.Contains(nav_type) && !allowedTypeNames.Contains(nav_type))
                                            collectedTypes.Add(nav_type);
                                    }
                                    else
                                    {
                                        discoverType(new TypeData(nav_type), doc, nsmanager, allowedTypes, allowedTypeNames, withNavPropertis, withKeys, false, collectedTypes);
                                    }
                                }
                                //else
                                //{
                                //    Console.WriteLine(typeName + ":" + FromRole + "/" + ToRole);
                                //}
                            }
                        }
                    }
                }
            }
        }


        static List<TypeData> DiscoverProperyDependencies(List<TypeData> types, XmlDocument doc, XmlNamespaceManager nsmanager, bool withNavPropertis, bool withKeys)
        {
            List<TypeData> allowedTypes = new List<TypeData>();
            List<string> allowedTypeNames = types.Select(t => t.Name).ToList();

            for (int i = 0; i < types.Count; i++)
            {
                discoverProperties(types[i], doc, nsmanager, allowedTypes, allowedTypeNames, withNavPropertis, withKeys);
            }

            return allowedTypes;
        }
        static void discoverProperties(TypeData typeData, XmlDocument doc, XmlNamespaceManager nsmanager, List<TypeData> allowedTypes, List<string> allowedTypeNames, bool withNavPropertis, bool withKeys)
        {
            string typeName = typeData.Name;
            Console.WriteLine("Discover: " + typeName);

            bool hasProperty = typeData.Fields.Count != 0;
            string typeShortName = typeName;
            string typeNamespace = string.Empty;
            if (typeName.LastIndexOf('.') > 0)
            {
                typeNamespace = typeName.Substring(0, typeName.LastIndexOf('.'));
                typeShortName = typeName.Substring(typeName.LastIndexOf('.') + 1);
            }

            var schemaNode = doc.SelectSingleNode("//edmx:Schema[@Namespace = '" + typeNamespace + "']", nsmanager);
            if (schemaNode != null)
            {
                var typeNode = schemaNode.SelectSingleNode("edmx:EntityType[@Name = '" + typeShortName + "'] | edmx:ComplexType[@Name = '" + typeShortName + "']", nsmanager);
                if (typeNode != null)
                {
                    allowedTypes.Add(typeData);

                    if (!hasProperty)
                    {
                        var properties = typeNode.SelectNodes("edmx:Property", nsmanager);
                        if (properties != null)
                        {
                            for (int j = 0; j < properties.Count; j++)
                            {
                                string field = properties[j].Attributes["Name"].Value;
                                typeData.Fields.Add(field);
                            }
                        }

                        if (withNavPropertis)
                        {
                            var navPropNodes = typeNode.SelectNodes("edmx:NavigationProperty", nsmanager);
                            for (int j = 0; j < navPropNodes.Count; j++)
                            {
                                var navProp = navPropNodes[j];
                                string nav_name = navProp.Attributes["Name"].Value;
                                string[] types = new string[] { navProp.Attributes["FromRole"].Value, navProp.Attributes["ToRole"].Value };

                                string nav_type = string.Empty;
                                for (int t = 0; t < types.Length; t++)
                                {
                                    var association = schemaNode.SelectSingleNode("edmx:Association/edmx:End[@Role = '" + types[t] + "']", nsmanager);
                                    if (association != null)
                                    {
                                        nav_type = association.Attributes["Type"].Value;
                                        if (nav_type != typeName || t == 1)
                                            break;
                                    }
                                }

                                if (allowedTypeNames.Contains(nav_type))
                                {
                                    typeData.Fields.Add(nav_name);
                                }
                            }
                        }
                    }
                    else if (withKeys)
                    {
                        var keys = typeNode.SelectNodes("edmx:Key/edmx:PropertyRef", nsmanager);
                        if (keys != null)
                        {
                            for (int j = 0; j < keys.Count; j++)
                            {
                                string keyField = keys[j].Attributes["Name"].Value;
                                if (!typeData.Fields.Contains(keyField))
                                    typeData.Fields.Insert(j, keyField);
                            }
                        }
                    }
                }
            }
        }
    }

    public class TypeData {
        public TypeData(string data)
        {
            if (data.Contains(':'))
            {
                string[] parts = data.Split(new char[]{':'}, StringSplitOptions.RemoveEmptyEntries);
                Name = parts[0];
                Fields = parts[1].Split(',').ToList();
            }
            else {
                Name = data;
                Fields = new List<string>();
            }
        }

        public string Name { get; set; }
        public List<string> Fields { get; set; }

        public bool isUsed(string field) {
            return this.Fields.Count() == 0 || this.Fields.Contains(field);
        }

    }
}
