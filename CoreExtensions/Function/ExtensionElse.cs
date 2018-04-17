﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TastyScript.IFunction.Attributes;
using TastyScript.IFunction.Extension;

namespace TastyScript.CoreExtensions.Function
{
    [Extension("Else", new string[] { "invoke" }, invoking: true, alias: new string[] { "else" })]
    [Serializable]
    public class ExtensionElse : BaseExtension { }
}