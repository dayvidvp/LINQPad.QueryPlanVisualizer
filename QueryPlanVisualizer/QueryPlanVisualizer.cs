﻿using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Xsl;

using LINQPad;

using Visualizer.Properties;

namespace Visualizer
{
    public static class QueryPlanVisualizer
    {
        public static void DumpPlan<T>(this IQueryable<T> queryable)
        {
            var sqlConnection = Util.CurrentDataContext.Connection as SqlConnection;

            if (sqlConnection == null)
            {
                var control = new Label { Text = "Query Plan Visualizer supports only Sql Server" };
                PanelManager.DisplayControl(control);
                return;
            }

            try
            {
                Util.CurrentDataContext.Connection.Open();

                using (var command = new SqlCommand("SET STATISTICS XML ON", sqlConnection))
                {
                    command.ExecuteNonQuery();
                }

                using (var reader = Util.CurrentDataContext.GetCommand(queryable).ExecuteReader())
                {
                    while (reader.NextResult())
                    {
                        if (reader.GetName(0) == "Microsoft SQL Server 2005 XML Showplan")
                        {
                            reader.Read();

                            var planHtml = ConvertPlanToHtml(reader.GetString(0));

                            var files = ExtractFiles();
                            files.Add(planHtml);

                            var html = string.Format(Resources.template, files.ToArray());
                            var webBrowser = new WebBrowser { DocumentText = html };

                            PanelManager.DisplayControl(webBrowser, "Query plan");

                            break;
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                var control = new Label { Text = exception.ToString() };
                PanelManager.DisplayControl(control);
            }
            finally
            {
                Util.CurrentDataContext.Connection.Close();
            }
        }

        private static string ConvertPlanToHtml(string planXml)
        {
            var schema = new XmlSchemaSet();
            using (var planSchemaReader = XmlReader.Create(new StringReader(Resources.showplanxml)))
            {
                schema.Add("http://schemas.microsoft.com/sqlserver/2004/07/showplan", planSchemaReader);
            }

            var transform = new XslCompiledTransform(true);

            using (var xsltReader = XmlReader.Create(new StringReader(Resources.qpXslt)))
            {
                transform.Load(xsltReader);
            }

            var planHtml = new StringBuilder();

            var settings = new XmlReaderSettings
            {
                ValidationType = ValidationType.Schema,
                Schemas = schema,
            };

            using (var queryPlanReader = XmlReader.Create(new StringReader(planXml), settings))
            {
                using (var writer = XmlWriter.Create(planHtml, transform.OutputSettings))
                {
                    transform.Transform(queryPlanReader, writer);
                }
            }
            return planHtml.ToString();
        }

        private static List<string> ExtractFiles()
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LINQPadQueryVisualizer");

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var qpJavascript = Path.Combine(folder, "qp.js");
            var qpStyleSheet = Path.Combine(folder, "qp.css");
            var jquery = Path.Combine(folder, "jquery.js");

            File.WriteAllText(qpJavascript, Resources.jquery);
            File.WriteAllText(qpStyleSheet, Resources.qpStyleSheet);
            File.WriteAllText(jquery, Resources.qpJavascript);

            return new List<string> { qpStyleSheet, qpJavascript, jquery };
        }
    }
}