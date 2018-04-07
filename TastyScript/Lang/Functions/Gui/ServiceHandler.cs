﻿using Nancy;
using System;
using Nancy.Extensions;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Nancy.IO;
using System.Dynamic;
using System.IO;
using Nancy.ModelBinding;
using Microsoft.CSharp.RuntimeBinder;
using TastyScript.Lang.Tokens;
using TastyScript.Lang.Extensions;

namespace TastyScript.Lang.Functions.Gui
{
    class ServiceHandler
    {
        public static string IndexPath;
    }

    public class IndexModule : NancyModule
    {
        public IndexModule() : base("")
        {
            Get(@"/", _ => 
            {
                return View[ServiceHandler.IndexPath];
            });
        }
    }
    
    public class PostTestModule : NancyModule
    {
        public PostTestModule() : base("")
        {
            Post(@"/Post", _ =>
            {
                var body = RequestStream.FromStream(this.Request.Body).AsString();
                Console.WriteLine(body);
                return body;
            });
        }
    }
    public class GetTestModule : NancyModule
    {
        public GetTestModule() : base("")
        {
            Get(@"/Get", _ =>
            {
                var model =
                    this.Bind<DynamicDictionary>();

                if (model.TryGetValue("Call", out dynamic callout))
                {
                    var func = FunctionStack.First((String)callout);
                    func.TryParse(new TFunction(func, new List<EDefinition>(), model.ToDictionary(), null, null));
                    return func.ReturnBubble?.ToString().UnCleanString();
                }
                else
                {
                    Console.WriteLine("borked");
                }
                return null;
            });
        }
    }

    public class DynamicModelBinder : IModelBinder
    {
        public object Bind(NancyContext context, Type modelType, object instance, BindingConfig configuration, params string[] blackList)
        {
            var data =
                GetDataFields(context);

            var model =
                DynamicDictionary.Create(data,null);

            return model;
        }

        private static IDictionary<string, object> GetDataFields(NancyContext context)
        {
            return Merge(new IDictionary<string, string>[]
            {
                ConvertDynamicDictionary(context.Request.Form),
                ConvertDynamicDictionary(context.Request.Query),
                ConvertDynamicDictionary(context.Parameters)
            });
        }

        private static IDictionary<string, object> Merge(IEnumerable<IDictionary<string, string>> dictionaries)
        {
            var output =
                new Dictionary<string, object>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var dictionary in dictionaries.Where(d => d != null))
            {
                foreach (var kvp in dictionary)
                {
                    if (!output.ContainsKey(kvp.Key))
                    {
                        output.Add(kvp.Key, kvp.Value);
                    }
                }
            }

            return output;
        }

        private static IDictionary<string, string> ConvertDynamicDictionary(DynamicDictionary dictionary)
        {
            return dictionary.GetDynamicMemberNames().ToDictionary(
                    memberName => memberName,
                    memberName => (string)dictionary[memberName]);
        }

        public bool CanBind(Type modelType)
        {
            return modelType == typeof(DynamicDictionary);
        }
    }
}
