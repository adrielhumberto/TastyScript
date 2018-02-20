﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TastyScript.Android;
using TastyScript.Lang.Exceptions;
using TastyScript.Lang.Token;

namespace TastyScript.Lang.Func
{
    public static class FunctionHelpers
    {
        public static void Sleep(double ms)
        {
            var func = new TFunction("Sleep", TokenParser.FunctionList.FirstOrDefault(f => f.Name == "Sleep"));
            var newArgs = new TParameter("sleep", new List<IBaseToken>() { new TNumber("sleep", ms) });
            func.Value.Value.TryParse(newArgs);
        }
        public static void AndroidTouch(int x, int y)
        {
            try
            {
                Program.AndroidDriver.Tap(x, y);
            }
            catch
            {
                Compiler.ExceptionListener.ThrowSilent(new ExceptionHandler(ExceptionType.DriverException, $"ADB returned null input. Sleeping 5 seconds and trying again"));
                Sleep(5000);
                AndroidTouch(x, y);
            }
        }
        public static void AndroidBack()
        {
            try
            {
                Program.AndroidDriver.KeyEvent(Android.AndroidKeyCode.KEYCODE_BACK);
            }
            catch
            {
                Compiler.ExceptionListener.ThrowSilent(new ExceptionHandler(ExceptionType.DriverException, $"ADB returned null input. Sleeping 5 seconds and trying again"));
                Sleep(5000);
                AndroidBack();
            }
        }
        public static void AndroidCheckScreen(string succPath, IBaseFunction succFunc, IBaseFunction failFunc, int thresh = 90)
        {
            AnalyzeScreen ascreen = new AnalyzeScreen();
            ascreen.Analyze(succPath,
                () => { succFunc.TryParse(null); },
                () => { failFunc.TryParse(null); },
                thresh
            );
        }
        public static void AndroidCheckScreen(string succPath, string failPath, IBaseFunction succFunc, IBaseFunction failFunc, int thresh = 90)
        {
            AnalyzeScreen ascreen = new AnalyzeScreen();
            ascreen.Analyze(succPath, failPath,
                () => { succFunc.TryParse(null); },
                () => { failFunc.TryParse(null); },
                thresh
            );
        }
    }
    public class FunctionDefinitions<T> : AnonymousFunction<T>, IOverride<T>
    {
        public virtual IFunction<T> CallBase(TParameter args) { return default(IFunction<T>); }
        public override void TryParse(TParameter args, string lineval = "{0}")
        {
            LineValue = lineval;
            var findFor = Extensions.FirstOrDefault(f => f.Name == "For") as ExtensionFor;
            if (findFor != null)
            {
                //if for extension exists, reroutes this tryparse method to the loop version without the for check
                ForExtension(args, findFor, lineval);
                return;
            }
            if (args != null)
            {
                var arg = args.Value.Value.FirstOrDefault(f => f.Name.Contains("[]")) as TVariable;
                var argarr = args.Value.Value.FirstOrDefault(f => f.Name.Contains("[]")) as TParameter;
                if (argarr != null)//if arg array is multi element
                {
                    ProvidedArgs = new List<IBaseToken>();
                    for (var i = 0; i < argarr.Value.Value.Count; i++)
                    {
                        ProvidedArgs.Add(new TVariable(ExpectedArgs[i], argarr.Value.Value[i]));
                    }
                }
                else if (arg != null)//if arg array is a single element 
                {
                    ProvidedArgs = new List<IBaseToken>();
                    ProvidedArgs.Add(new TVariable(ExpectedArgs[0], arg.Value.Value));
                }
                else
                {
                    ProvidedArgs = new List<IBaseToken>();
                    for (var i = 0; i < args.Value.Value.Count; i++)
                    {
                        ProvidedArgs.Add(new TVariable(ExpectedArgs[i], args.Value.Value[i]));
                    }
                }
            }
            Parse(args);
        }
        public override void TryParse(TParameter args, bool forFlag, string lineval = "{0}")
        {
            LineValue = lineval;
            if (args != null)
            {
                var arg = args.Value.Value.FirstOrDefault(f => f.Name.Contains("[]")) as TVariable;
                var argarr = args.Value.Value.FirstOrDefault(f => f.Name.Contains("[]")) as TParameter;
                if (argarr != null)//if arg array is multi element
                {
                    ProvidedArgs = new List<IBaseToken>();
                    for (var i = 0; i < argarr.Value.Value.Count; i++)
                    {
                        ProvidedArgs.Add(new TVariable(ExpectedArgs[i], argarr.Value.Value[i]));
                    }
                }
                else if (arg != null)//if arg array is a single element 
                {
                    ProvidedArgs = new List<IBaseToken>();
                    ProvidedArgs.Add(new TVariable(ExpectedArgs[0], arg.Value.Value));
                }
                else
                {
                    ProvidedArgs = new List<IBaseToken>();
                    for (var i = 0; i < args.Value.Value.Count; i++)
                    {
                        ProvidedArgs.Add(new TVariable(ExpectedArgs[i], args.Value.Value[i]));
                    }
                }
            }
            Parse(args);
        }
        [Obsolete("this feels too redundant",true)]
        public virtual void TryCallBase(TParameter args)
        {
        }
    }

    [Function("TakeScreenshot", new string[] { "path" })]
    public class FunctionTakeScreenshot : FunctionDefinitions<object>
    {
        public override object Parse(TParameter args)
        {
            return CallBase(args);
        }
        public new object CallBase(TParameter args)
        {
            var path = ProvidedArgs.FirstOrDefault(f => f.Name == "path");
            if (path == null)
            {
                Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.NullReferenceException, $"Path must be specified", LineValue));
                return null;
            }
            var ss = Program.AndroidDriver.GetScreenshot();
            ss.Save(path.ToString(), ImageFormat.Png);
            return args;
        }
    }
    [Function("CheckScreen", new string[] { "succFunc", "failFunc", "succPath", "failPath" })]
    public class FunctionCheckScreen : FunctionDefinitions<object>
    {
        public override object Parse(TParameter args)
        {
            return CallBase(args);
        }
        public new object CallBase(TParameter args)
        {
            var succFunc = ProvidedArgs.FirstOrDefault(f => f.Name == "succFunc");
            var failFunc = ProvidedArgs.FirstOrDefault(f => f.Name == "failFunc");
            var succPath = ProvidedArgs.FirstOrDefault(f => f.Name == "succPath");
            var failPath = ProvidedArgs.FirstOrDefault(f => f.Name == "failPath");
            if (succFunc == null || failFunc == null || succPath == null)
            {
                Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.NullReferenceException, $"Invoke function cannot be null.", LineValue));
                return null;
            }
            var sf = TokenParser.FunctionList.FirstOrDefault(f => f.Name == succFunc.ToString());
            var ff = TokenParser.FunctionList.FirstOrDefault(f => f.Name == failFunc.ToString());
            if (sf == null || ff == null)
            {
                Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.CompilerException, $"Invoke function cannot be found.", LineValue));
                return null;
            }
            //check for threshold extension
            var threshExt = Extensions.FirstOrDefault(f => f.Name == "Threshold") as ExtensionThreshold;
            int thresh = 90;
            if(threshExt != null)
            {
                var param = threshExt.Extend();
                var nofail = int.TryParse(param.Value.Value[0].ToString(), out thresh);
            }
            if (failPath != null)
            {
                FunctionHelpers.AndroidCheckScreen(succPath.ToString(), failPath.ToString(), sf, ff,thresh);
            }
            else
            {
                FunctionHelpers.AndroidCheckScreen(succPath.ToString(), sf, ff,thresh);
            }

            return args;
        }
    }
    [Function("ConnectDevice", new string[] { "serial" })]
    public class FunctionConnectDevice : FunctionDefinitions<object>
    {
        public override object Parse(TParameter args)
        {
            return CallBase(args);
        }
        public new object CallBase(TParameter args)
        {
            var print = "";
            var argsList = ProvidedArgs.FirstOrDefault(f => f.Name == "serial");
            if (argsList != null)
                print = argsList.ToString();
            Program.AndroidDriver = new Android.Driver(print);

            return args;
        }
        protected override void ForExtension(TParameter args, ExtensionFor findFor, string lineval)
        {
            Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.CompilerException, $"Cannot call 'For' on {this.Name}.", LineValue));
        }
    }
    [Function("Loop", new string[] { "invoke" })]
    public class FunctionLoop : FunctionDefinitions<object>
    {
        public override object Parse(TParameter args)
        {
            return CallBase(args);
        }
        public new object CallBase(TParameter args)
        {
            var prov = ProvidedArgs.FirstOrDefault(f => f.Name == "invoke");
            if (prov == null)
                Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.CompilerException, $"Invoke function cannot be null.", LineValue));
            var func = TokenParser.FunctionList.FirstOrDefault(f => f.Name == prov.ToString());
            if (func == null)
                Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.CompilerException, $"Invoke function cannot be null.", LineValue));
            var findFor = Extensions.FirstOrDefault(f => f.Name == "For") as ExtensionFor;
            if (findFor == null)
                Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.CompilerException, $"Infinite loops are not supported.", LineValue));
            TParameter forNumber = findFor.Extend();
            int forNumberAsNumber = int.Parse(forNumber.Value.Value[0].ToString());
            for (var x = 0; x <= forNumberAsNumber; x++)
            {
                //gave a string as the parameter because number was causing srs problems
                if (!TokenParser.Stop)
                    func.TryParse(new TParameter("Loop", new List<IBaseToken>() { new TString("Enumerator", x.ToString()) }));
            }
            return args;
        }
        //stop the base for looping extension from overriding this custom looping function
        protected override void ForExtension(TParameter args, ExtensionFor findFor, string lineval)
        {
            TryParse(args, false, lineval);
        }
    }
    [Function("SetDefaultSleep", new string[] { "sleep" })]
    public class FunctionSetDefaultSleep : FunctionDefinitions<object>
    {
        public override object Parse(TParameter args)
        {
            return CallBase(args);
        }
        public new object CallBase(TParameter args)
        {
            var sleep = (ProvidedArgs.FirstOrDefault(f => f.Name == "sleep") as TVariable).Value.Value as TNumber;
            double sleepnum = sleep.Value.Value;
            TokenParser.SleepDefaultTime = sleepnum;
            return args;
        }
        protected override void ForExtension(TParameter args, ExtensionFor findFor, string lineval)
        {
            Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.CompilerException, $"Cannot call 'For' on {this.Name}.", LineValue));
        }
    }
    [Function("If", new string[] { "bool" })]
    public class FunctionIf : FunctionDefinitions<object>
    {
        public override object Parse(TParameter args)
        {
            return CallBase(args);
        }
        public new object CallBase(TParameter args)
        {
            var prov = ProvidedArgs.FirstOrDefault(f => f.Name == "bool");
            if (prov == null)
                Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.CompilerException, $"Arguments cannot be null.", LineValue));
            if (prov.ToString() == "True")
            {
                //find then extension and call it
                var test = JsonConvert.SerializeObject(Extensions);
                var findThen = Extensions.FirstOrDefault(f => f.Name == "Then") as ExtensionThen;
                if (findThen != null)
                {
                    TParameter thenFunc = findThen.Extend();
                    var func = TokenParser.FunctionList.FirstOrDefault(f => f.Name == thenFunc.Value.Value[0].ToString());
                    if (func == null)
                        Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.CompilerException, $"Cannot find the invoked function.", LineValue));
                    func.TryParse(null);
                }
            }
            else
            {
                //find else extension and call it
                var findElse = Extensions.FirstOrDefault(f => f.Name == "Else") as ExtensionElse;
                if (findElse != null)
                {
                    TParameter elseFunc = findElse.Extend();
                    var func = TokenParser.FunctionList.FirstOrDefault(f => f.Name == elseFunc.Value.Value[0].ToString());
                    if (func == null)
                        Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.CompilerException, $"Cannot find the invoked function.", LineValue));
                    func.TryParse(null);
                }
            }
            return args;
        }
    }
    [Function("Start")]
    public class FunctionStart : FunctionDefinitions<object>
    {
        public override void TryParse(TParameter args, string lineval = "{0}")
        {
            Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.CompilerException, $"{this.Name} can only be called by internal functions.", LineValue));
      
        }
        public new object CallBase(TParameter args)
        {
            Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.CompilerException, $"{this.Name} can only be called by internal functions.", LineValue));
            return null;
        }
        protected override void ForExtension(TParameter args, ExtensionFor findFor, string lineval)
        {
            Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.CompilerException, $"Cannot call 'For' on {this.Name}.", LineValue));
        }
    }
    [Function("Halt")]
    public class FunctionHalt : FunctionDefinitions<object>
    {
        public override void TryParse(TParameter args, string lineval = "{0}")
        {
            Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.CompilerException, $"{this.Name} can only be called by internal functions.", LineValue));
        }
        public new object CallBase(TParameter args)
        {
            Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.CompilerException, $"{this.Name} can only be called by internal functions.", LineValue));
            return null;
        }
        protected override void ForExtension(TParameter args, ExtensionFor findFor, string lineval)
        {
            Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.CompilerException, $"Cannot call 'For' on {this.Name}.", LineValue));
        }
    }
    [Function("Awake", FunctionObsolete: true)]
    public class FunctionAwake : FunctionDefinitions<object>
    {
        public override void TryParse(TParameter args, string lineval = "{0}")
        {
        }
        public new object CallBase(TParameter args)
        {
            Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.CompilerException, $"{this.Name} can only be called by internal functions.", LineValue));
            return null;
        }
        protected override void ForExtension(TParameter args, ExtensionFor findFor, string lineval)
        {
            Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.CompilerException, $"Cannot call 'For' on {this.Name}.", LineValue));
        }
    }
    [Function("Base")]
    public class FunctionBase : FunctionDefinitions<object>
    {
        public override void TryParse(TParameter args, string lineval = "{0}")
        {
            Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.CompilerException, $"{this.Name} can not be overrided", LineValue));
        }
        public new object CallBase(TParameter args)
        {
            Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.CompilerException, $"{this.Name} can not be overrided.", LineValue));
            return null;
        }
        protected override void ForExtension(TParameter args, ExtensionFor findFor, string lineval)
        {
            Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.CompilerException, $"Cannot call 'For' on {this.Name}.", LineValue));
        }
    }
    [Function("Sleep", new string[] { "time" })]
    public class FunctionSleep : FunctionDefinitions<object>
    {
        public override object Parse(TParameter args)
        {
            return CallBase(args);
        }
        public new object CallBase(TParameter args)
        {
            var time = double.Parse((ProvidedArgs.FirstOrDefault(f => f.Name == "time") as TVariable).Value.Value.ToString());
            Thread.Sleep((int)Math.Ceiling(time));
            return args;
        }
    }
    [Function("Touch", new string[] { "intX", "intY", "sleep" })]
    public class FunctionTouch : FunctionDefinitions<object>
    {
        public override object Parse(TParameter args)
        {
            return CallBase(args);
        }
        public new object CallBase(TParameter args)
        {
            var x = (ProvidedArgs.FirstOrDefault(f => f.Name == "intX") as TVariable);
            var y = (ProvidedArgs.FirstOrDefault(f => f.Name == "intY") as TVariable);
            double intX = double.Parse(x.Value.Value.ToString());
            double intY = double.Parse(y.Value.Value.ToString());
            if (Program.AndroidDriver == null)
                IO.Output.Print($"[DRIVERLESS] Touch x:{intX} y:{intY}");
            else
                FunctionHelpers.AndroidTouch((int)intX, (int)intY);
            double sleep = TokenParser.SleepDefaultTime;
            if (args.Value.Value.Count > 2)
            {
                sleep = double.Parse((ProvidedArgs.FirstOrDefault(f => f.Name == "sleep") as TVariable).Value.Value.ToString());
            }
            FunctionHelpers.Sleep(sleep);
            return args;
        }
    }
    [Function("Back")]
    public class FunctionBack : FunctionDefinitions<object>
    {
        public override object Parse(TParameter args)
        {
            return CallBase(args);
        }
        public new object CallBase(TParameter args)
        {
            if (Program.AndroidDriver != null)
            {
                FunctionHelpers.AndroidBack();
            }
            else
            {
                IO.Output.Print($"[DRIVERLESS] Back Button Keyevent");
            }
            return args;
        }
    }
    [Function("PrintLine", new string[] { "s", "color" })]
    public class FunctionPrintLine : FunctionDefinitions<string>
    {
        public override string Parse(TParameter args)
        {
            AddParams();
            return CallBase(args);
        }
        private List<string> addParamsStrings = new List<string>();
        private void AddParams()
        {
            var findAddParams = Extensions.FirstOrDefault(f => f.Name == "AddParams") as ExtensionAddParams;
            if (findAddParams != null)
            {
                var addParamsList = Extensions.Where(f => f.Name == "AddParams");
                foreach (var x in addParamsList)
                {
                    var param = x as ExtensionAddParams;
                    TParameter ext = param.Extend();
                    addParamsStrings.Add(ext.Value.Value[0].ToString());
                }
            }
        }
        public new string CallBase(TParameter args)
        {
            var print = "";
            var argsList = ProvidedArgs.FirstOrDefault(f => f.Name == "s");
            if (argsList != null)
                print = argsList.ToString();
            //color extension check
            var color = ConsoleColor.Gray;
            var findColorExt = Extensions.FirstOrDefault(f => f.Name == "Color") as ExtensionColor;
            if(findColorExt != null)
            {
                var param = findColorExt.Extend();
                ConsoleColor newcol = ConsoleColor.Gray;
                var nofail = Enum.TryParse<ConsoleColor>(param.Value.Value[0].ToString(), out newcol);
                if (nofail)
                    color = newcol;
            }
            IO.Output.Print(print + String.Join("", addParamsStrings),color);

            //clear extensions after done
            addParamsStrings = new List<string>();
            Extensions = new List<IExtension>();
            return print;
        }
    }
    [Function("Print", new string[] { "s" })]
    public class FunctionPrint : FunctionDefinitions<string>
    {
        public override string Parse(TParameter args)
        {
            AddParams();
            return CallBase(args);
        }
        private List<string> addParamsStrings = new List<string>();
        private void AddParams()
        {
            var findAddParams = Extensions.FirstOrDefault(f => f.Name == "AddParams") as ExtensionAddParams;
            if (findAddParams != null)
            {
                var addParamsList = Extensions.Where(f => f.Name == "AddParams");
                foreach (var x in addParamsList)
                {
                    var param = x as ExtensionAddParams;
                    TParameter ext = param.Extend();
                    addParamsStrings.Add(ext.Value.Value[0].ToString());
                }
            }
        }
        public new string CallBase(TParameter args)
        {
            var print = "";
            var argsList = ProvidedArgs.FirstOrDefault(f => f.Name == "s");
            if (argsList != null)
                print = argsList.ToString();

            //color extension check
            var color = ConsoleColor.Gray;
            var findColorExt = Extensions.FirstOrDefault(f => f.Name == "Color") as ExtensionColor;
            if (findColorExt != null)
            {
                var param = findColorExt.Extend();
                ConsoleColor newcol = ConsoleColor.Gray;
                var nofail = Enum.TryParse<ConsoleColor>(param.Value.Value[0].ToString(), out newcol);
                if (nofail)
                    color = newcol;
            }

            IO.Output.Print(print + String.Join("", addParamsStrings),color,false);

            //clear extensions after done
            addParamsStrings = new List<string>();
            Extensions = new List<IExtension>();
            return print;
        }
    }
}