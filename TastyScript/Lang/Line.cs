﻿using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TastyScript.Lang.Extensions;
using TastyScript.Lang.Tokens;

namespace TastyScript.Lang
{
    internal class Line
    {
        private IBaseFunction _reference;
        public string Value { get; private set; }
        //public TFunction Token { get; private set; }

        public Line(string val, IBaseFunction reference)
        {
            Compiler.ExceptionListener.SetCurrentLine(val);
            Value = val;
            _reference = reference;
            //Token = 
                WalkTree(val);
            
        }
        private string ReplaceAllNotInStringWhiteSpace(string value)
        {
            bool level = false;
            string output = "";
            for (int i = 0; i < value.Length; i++)
            {
                if(value[i] == '\"')
                {
                    level = (level) ? false : true;
                }
                if (level)
                {   
                    //these symbols are for preserving strings.
                    switch (value[i])
                    {
                        case ('+'):
                            output += "&plus;";
                            break;
                        case ('-'):
                            output += "&neg;";
                            break;
                        case ('='):
                            output += "&eq;";
                            break;
                        case ('%'):
                            output += "&per;";
                            break;
                        case ('$'):
                            output += "&dollar;";
                            break;
                        case ('!'):
                            output += "&expl;";
                            break;
                        case ('('):
                            output += "&lparen;";
                            break;
                        case (')'):
                            output += "&rparen;";
                            break;
                        case ('['):
                            output += "&lbrack;";
                            break;
                        case (']'):
                            output += "&rbrack;";
                            break;
                        case ('{'):
                            output += "&lbrace;";
                            break;
                        case ('}'):
                            output += "&rbrace;";
                            break;
                        case ('<'):
                            output += "&lchev;";
                            break;
                        case ('>'):
                            output += "&rchev;";
                            break;
                        case ('.'):
                            output += "&period;";
                            break;
                        case ('&'):
                            output += "&amp;";
                            break;
                        case (','):
                            output += "&comma;";
                            break;
                        default:
                            output += value[i];
                            break;
                    }
                }
                else
                {
                    if (value[i] != ' ' && value[i] != '\n' && value[i] != '\r' && value[i] != '\t')
                    {
                        output += value[i];
                    }
                }
            }
            return output;
        }
        private void WalkTree(string value)
        {
            //value = value.Replace("\r", "").Replace("\n", "").Replace("\t", "");
            value = value.ReplaceFirst("var ", "var%");
            value = ReplaceAllNotInStringWhiteSpace(value);
            //TFunction temp = null;
            value = ParseMathExpressions(value);
            value = ParseArrays(value);
            value = ParseParameters(value);
            value = ParseStrings(value);
            value = ParseNumbers(value);
            value = value.Replace(".", "<-").Replace("\n", "").Replace("\r", "").Replace("\t", "");
            //check for empty lines
            var wscheck = new Regex(@"^\s*$");
            var wscheckk = wscheck.IsMatch(value);
            if (wscheckk)
                return;// temp;
            //
            //get var extensions before normal extensions
            
            
            //vars here
            if (value.Contains("var%"))
            {
                value = EvaluateVar(value);
                if (value == "")
                    return;// temp;
            }
            //try extension sweep after vars instead of before
            //value = EvaluateVarExtensions(value);
            var ext = ParseExtensions(value);

            //
            //temp =
            ParseFunctions(value, ext);
            return;// temp;
        }
        private string ParseStrings(string value)
        {
            var stringTokenRegex = new Regex("\"([^\"\"]*)\"", RegexOptions.Multiline);
            var strings = stringTokenRegex.Matches(value);
            foreach (var x in strings)
            {
                string tokenname = "{AnonGeneratedToken" + TokenParser.AnonymousTokensIndex + "}";
                var tstring = new Token(tokenname, Regex.Replace(x.ToString(), "\"", ""),Value);
                value = value.Replace(x.ToString(), tokenname);

                TokenParser.AnonymousTokens.Add(tstring);
            }
            return value;
        }
        private string ParseNumbers(string value)
        {
            var numberTokenRegex = new Regex(@"\b-*[0-9\.]+\b", RegexOptions.Multiline);
            var numbers = numberTokenRegex.Matches(value);
            foreach (var x in numbers)
            {
                string tokenname = "{AnonGeneratedToken" + TokenParser.AnonymousTokensIndex + "}";
                double output = 0;
                var nofail = double.TryParse(x.ToString(), out output);
                if (nofail)
                {
                    TokenParser.AnonymousTokens.Add(new Token(tokenname, output.ToString(),Value));
                    //do this regex instead of a blind replace to fix the above issue. 
                    //NOTE this fix may break decimal use in some situations!!!!
                    var indvRegex = (@"\b-*" + x + @"\b");
                    var regex = new Regex(indvRegex);
                    value = regex.Replace(value, tokenname);
                }
            }
            return value;
        }
        private string ParseMathExpressions(string value)
        {
            var mathexpRegex = new Regex(@"\[([^\[\]]*)\]", RegexOptions.Multiline);
            var mathexp = mathexpRegex.Matches(value);
            foreach (var x in mathexp)
            {
                var input = x.ToString().Replace("[", "").Replace("]", "").Replace(" ", "");
                if (input != null && input != "")
                {
                    string tokenname = "{AnonGeneratedToken" + TokenParser.AnonymousTokensIndex + "}";
                    double exp = MathExpression(input);
                    TokenParser.AnonymousTokens.Add(new Token(tokenname, exp.ToString(), Value));
                    value = value.Replace(x.ToString(), tokenname);
                }
            }
            return value;
        }
        private string ParseParameters(string value)
        {
            string val = value;
            //first we have to find all the arrays
            var regstr = @"(?:(?:\((?>[^()]+|\((?<number>)|\)(?<-number>))*(?(number)(?!))\))|{^()\))+";
            var arrayRegex = new Regex(regstr, RegexOptions.Multiline);
            //var arrayRegex = new Regex(@"\(([^()]*)\)", RegexOptions.Multiline);
            var arrayMatches = arrayRegex.Matches(val);
            foreach (var a in arrayMatches)
            {
                var param = a.ToString().Substring(1, a.ToString().Length - 2);
                if(param.Contains("(") && param.Contains(")"))
                {
                    param = ParseParameters(param);
                    param = param.Replace(".", "<-").Replace("\n", "").Replace("\r", "").Replace("\t", "");
                    param = EvaluateVarExtensions(param);
                    
                    var fext = ParseExtensions(param);
                    var fcheckSplit = param.Split(new string[] { "->" }, StringSplitOptions.None);
                    var fcheck = TokenParser.FunctionList.FirstOrDefault(f => f.Name == fcheckSplit[0]);
                    if(fcheck != null)
                        param = ParseFunctions(param, fext);
                    
                }
                string tokenname = "{AnonGeneratedToken" + TokenParser.AnonymousTokensIndex + "}";
                
                var compCheck = ComparisonCheck(param);
                if (compCheck != "")
                {
                    TokenParser.AnonymousTokens.Add(new Token(tokenname, compCheck, val));
                }
                else
                {
                    var commaRegex = new Regex(",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");
                    var commaSplit = commaRegex.Split(param);
                    var tokens = GetTokens(commaSplit, true);
                    //make sure values are being collected and not tokens
                    if (tokens.Count > 0)
                    {
                        for (int i = 0; i < commaSplit.Length; i++)
                        {
                            var obj = tokens.FirstOrDefault(f => f.Name == commaSplit[i]);
                            if (obj != null)
                                commaSplit[i] = obj.Value;
                        }
                        param = string.Join(",", commaSplit);
                    }
                    TokenParser.AnonymousTokens.Add(new Token(tokenname, param, val));
                }
                val = val.Replace(a.ToString(), "->" + tokenname + "|");
            }
            return val;
        }
        private string ParseArrays(string value)
        {
            string val = value;
            if (val.Contains("array("))
            {
                //first we have to find all the arrays
                var arrayRegex = new Regex(@"\barray\(([^()]*)\)", RegexOptions.Multiline);
                var arrayMatches = arrayRegex.Matches(val);
                foreach (var a in arrayMatches)
                {
                    string tokenname = "{AnonGeneratedToken" + TokenParser.AnonymousTokensIndex + "}";
                    var param = a.ToString().Replace("array(", "").Replace(")", "");
                    var compCheck = ComparisonCheck(param);
                    if (compCheck != "")
                    {
                        TokenParser.AnonymousTokens.Add(new Token(tokenname, compCheck, val));
                    }
                    else
                    {
                        var commaRegex = new Regex(",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");
                        var commaSplit = commaRegex.Split(param);
                        var tokens = GetTokens(commaSplit, true);
                        //make sure values are being collected and not tokens
                        if (tokens.Count > 0)
                        {
                            for (int i = 0; i < commaSplit.Length; i++)
                            {
                                var obj = tokens.FirstOrDefault(f => f.Name == commaSplit[i]);
                                if (obj != null)
                                    commaSplit[i] = obj.Value;
                            }
                            param = string.Join(",", commaSplit);
                        }
                        TokenParser.AnonymousTokens.Add(new TArray(tokenname, commaSplit, val));
                    }
                    val = val.Replace(a.ToString(), "" + tokenname + "");
                }
            }
            return val;
        }
        private List<EDefinition> ParseExtensions(string value)
        {
            List<EDefinition> temp = new List<EDefinition>();
            if (value.Contains("<-"))
            {
                string val = value;
                var firstSplit = val.Split(new string[] { "<-" }, StringSplitOptions.None);
                for (int i = 0; i < firstSplit.Length; i++) 
                {
                    if (i == 0)
                        continue;
                    var first = firstSplit[i];
                    var secondSplit = first.Split(new string[] { "->" }, StringSplitOptions.None);
                    if (secondSplit.Length != 2)
                        Compiler.ExceptionListener.Throw("[160]Extensions must provide arguments",ExceptionType.SyntaxException);
                    var original = TokenParser.Extensions.FirstOrDefault(f => f.Name == secondSplit[0]);
                    if (original == null)
                        Compiler.ExceptionListener.Throw($"[310]Cannot find extension [{secondSplit[0]}]");
                    //Console.WriteLine(secondSplit[0] + " " + secondSplit[1]);
                    var clone = DeepCopy<EDefinition>(original);
                    var param = GetTokens(new string[] { secondSplit[1].Replace("|", "") });
                    if (param.Count != 1)
                        Compiler.ExceptionListener.Throw("[166]Extensions must provide arguments", ExceptionType.SyntaxException);
                    if (clone.Invoking)
                    {
                        var invokeFuncName = param[0].ToString();
                        if (invokeFuncName.Contains("AnonymousFunction"))
                        {
                            var functionToInvoke = TokenParser.FunctionList.FirstOrDefault(f => f.Name == invokeFuncName.Replace("\"",""));
                            
                            if (functionToInvoke != null)
                            {
                                //Console.WriteLine($"n: {functionToInvoke.Name} exp: {string.Join(",",functionToInvoke.ExpectedArgs)}");
                                var args = GetTokens(functionToInvoke.ExpectedArgs, true, true);
                                List<string> argsarr = new List<string>();
                                foreach(var x in args)
                                {
                                    argsarr.Add(x.ToString());
                                }
                                clone.SetInvokeProperties(argsarr.ToArray());
                            }
                        }
                    }
                    clone.SetArguments(param[0].ToString());
                    temp.Add(clone);
                }
            }
            return temp;
        }
        private string ParseFunctions(string value, List<EDefinition> ext, bool safelook = false)
        {
            TFunction temp = null;
            string val = value;
            var firstSplit = value.Split('|')[0];
            var secondSplit = firstSplit.Split(new string[] { "->" }, StringSplitOptions.None);
            var func = TokenParser.FunctionList.FirstOrDefault(f => f.Name == secondSplit[0]);
            if (func == null)
            {
                if (safelook)
                    return "";
                else
                    Compiler.ExceptionListener.Throw($"[181]Cannot find function [{secondSplit[0]}]", ExceptionType.SyntaxException);
            }
            //get args
            var param = GetTokens(new string[] { secondSplit[1] });
            if (param.Count != 1)
                Compiler.ExceptionListener.Throw("[185]Extensions must provide arguments", ExceptionType.SyntaxException);
            if (func.Invoking)
            {
                var invokeFuncName = param[0].ToString();
                if (invokeFuncName.Contains("AnonymousFunction"))
                {
                    var functionToInvoke = TokenParser.FunctionList.FirstOrDefault(f => f.Name == invokeFuncName.Replace("\"", ""));
                    if (functionToInvoke != null)
                    {
                        var args = GetTokens(functionToInvoke.ExpectedArgs, true, true);
                        List<string> argsarr = new List<string>();
                        foreach (var x in args)
                        {
                            argsarr.Add(x.ToString());
                        }
                        func.SetInvokeProperties(argsarr.ToArray());
                    }
                }
            }
            var returnObj = new TFunction(func, ext, param[0].ToString(),_reference);
            temp = returnObj;
            //do the whole returning thing
            var getret = Parse(temp);
            if (getret != null)
            {
                string tokenname = "{AnonGeneratedToken" + TokenParser.AnonymousTokensIndex + "}";
                getret.SetName(tokenname);
                TokenParser.AnonymousTokens.Add(getret);
                val = tokenname;
                return val;
            }
            return "null";
        }

        private List<Token> GetTokens(string[] names, bool safe = false, bool returnInput = false, bool nullable = false)
        {
            List<Token> temp = new List<Token>();
            foreach (var n in names)
            {
                var stripws = n.Replace(" ", "");
                var tryLocal = _reference.LocalVariables.FirstOrDefault(f => f.Name == stripws);
                if (tryLocal != null)
                {
                    temp.Add(new Token(stripws, tryLocal.ToString(),Value));
                    continue;
                }
                var tryGlobal = TokenParser.GlobalVariables.FirstOrDefault(f => f.Name == stripws);
                if (tryGlobal != null)
                {
                    temp.Add(new Token(stripws, tryGlobal.ToString(), Value));
                    continue;
                }
                var tryAnon = TokenParser.AnonymousTokens.FirstOrDefault(f => f.Name == stripws);
                if (tryAnon != null)
                {
                    temp.Add(new Token(stripws, tryAnon.ToString(), Value));
                    continue;
                }
                //try params?
                var tryParams = _reference.ProvidedArgs.FirstOrDefault(f => f.Name == stripws);
                if(tryParams != null)
                {
                    temp.Add(new Token(stripws, tryParams.ToString(), Value));
                    continue;
                }
                if (returnInput)
                {
                    //temp.Add(new Token(stripws, stripws, Value));
                    double number = 0;
                    bool isNumeric = double.TryParse("123", out number);
                    if (isNumeric)
                        temp.Add(new Tokens.Token(stripws, stripws, Value));
                    else if (stripws.Contains("\""))
                        temp.Add(new Tokens.Token(stripws, stripws, Value));
                    else
                        temp.Add(new Tokens.Token(stripws, "null", Value));
                }
            }

            if (temp.Count == 0 && !safe)
            {
                //throw new Exception();
                Compiler.ExceptionListener.Throw($"Cannot find tokens [{string.Join(",", names)}]");
            }
            return temp;
        }
        private double MathExpression(string expression)
        {
            string exp = expression;
            //get vars and params out of the expression
            
            var varRegex = new Regex(@"\w[A-Za-z]*\d*");
            var varRegexMatches = varRegex.Matches(exp);
            foreach (var x in varRegexMatches)
            {
                var tok = GetTokens(new string[] { x.ToString() },true);

                var tokfirst = tok.FirstOrDefault(f => f != null);
                if (tokfirst != null)
                {
                    exp = exp.Replace(x.ToString(), tokfirst.ToString());
                }
            }
            try
            {
                DataTable table = new DataTable();
                table.Columns.Add("expression", typeof(string), exp);
                DataRow row = table.NewRow();
                table.Rows.Add(row);
                return double.Parse((string)row["expression"]);
            }
            catch (Exception e)
            {
                Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.SyntaxException,
                    $"[331]Unexpected error with mathematical expression:\n{e.Message}",
                    Value));
            }
            return 0;
        }
        #region Comparison
        enum Operator { EQ, NOTEQ, GT, LT, GTEQ, LTEQ }
        private string ComparisonCheck(string line)
        {
            string output = "";
            if (line.Contains("=="))
                output = FindOperation(Operator.EQ, line);
            else if (line.Contains("!="))
                output = FindOperation(Operator.NOTEQ, line);
            else if (line.Contains(">="))
                output = FindOperation(Operator.GTEQ, line);
            else if (line.Contains("<="))
                output = FindOperation(Operator.LTEQ, line);
            else if (line.Contains(">"))
                output = FindOperation(Operator.GT, line);
            else if (line.Contains("<"))
                output = FindOperation(Operator.LT, line);
            return output;
        }
        //the heavy lifting for comparison check
        private string FindOperation(Operator op, string line)
        {
            string output = "";
            string opString = "";
            switch (op)
            {
                case (Operator.EQ):
                    opString = "==";
                    break;
                case (Operator.NOTEQ):
                    opString = "!=";
                    break;
                case (Operator.GT):
                    opString = ">";
                    break;
                case (Operator.LT):
                    opString = "<";
                    break;
                case (Operator.GTEQ):
                    opString = ">=";
                    break;
                case (Operator.LTEQ):
                    opString = "<=";
                    break;
            }
            var splitop = line.Split(new string[] { opString }, StringSplitOptions.None);
            var lr = GetTokens(new string[] { splitop[0], splitop[1] },true,true);
            if (lr.Count != 2)
                Compiler.ExceptionListener.Throw("There must be one left-hand and one right-hand in comparison objects.",
                    ExceptionType.SyntaxException);
            try
            {
                switch (op)
                {
                    case (Operator.EQ):
                        output = (lr[0].ToString() == lr[1].ToString())
                            ? "True" : "False";
                        break;
                    case (Operator.NOTEQ):
                        output = (lr[0].ToString() != lr[1].ToString())
                            ? "True" : "False";
                        break;
                    case (Operator.GT):
                        output = (double.Parse(lr[0].ToString()) > double.Parse(lr[1].ToString()))
                            ? "True" : "False";
                        break;
                    case (Operator.LT):
                        output = (double.Parse(lr[0].ToString()) < double.Parse(lr[1].ToString()))
                            ? "True" : "False";
                        break;
                    case (Operator.GTEQ):
                        output = (double.Parse(lr[0].ToString()) >= double.Parse(lr[1].ToString()))
                            ? "True" : "False";
                        break;
                    case (Operator.LTEQ):
                        output = (double.Parse(lr[0].ToString()) <= double.Parse(lr[1].ToString()))
                            ? "True" : "False";
                        break;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.SyntaxException, $"Unexpected input: {line}"));
            }

            return output;
        }
        //this rips off the comparison check, since the concept is the same.
        private void CompareFail(string line)
        {
            Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.SyntaxException, $"Can not compare more or less than 2 values", line));
        }
        #endregion
        public static T DeepCopy<T>(T obj)
        {
            if (!typeof(T).IsSerializable)
            {
                throw new Exception("The source object must be serializable");
            }

            if (Object.ReferenceEquals(obj, null))
            {
                throw new Exception("The source object must not be null");
            }
            T result = default(T);
            using (var memoryStream = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(memoryStream, obj);
                memoryStream.Seek(0, SeekOrigin.Begin);
                result = (T)formatter.Deserialize(memoryStream);
                memoryStream.Close();
            }
            return result;

        }

        private string EvaluateVarExtensions(string val)
        {
            var value = val;
            if (val.Contains("<-"))
            {
                //get extensions
                var ext = ParseExtensions(value);
                //get object to be extended
                var strip = value.Split(new string[] { "<-" }, StringSplitOptions.None);
                var objLeft = strip[0];
                var objRemoveKeywords = objLeft.Split(new string[] { "+=", "-=", "++", "--", "=" }, StringSplitOptions.RemoveEmptyEntries);
                var obj = objRemoveKeywords[objRemoveKeywords.Length - 1];
                var objVar = GetTokens(new string[] { obj.Replace("|", "") }, true).FirstOrDefault();
                if (objVar != null)
                {
                    foreach (var e in ext)
                    {
                        if (e.VariableExtension)
                        {
                            string tokenname = "{AnonGeneratedToken" + TokenParser.AnonymousTokensIndex + "}";
                            var extobj = e.Extend(objVar);
                            if (extobj == null)
                                Compiler.ExceptionListener.Throw($"[610]Unexpected error compiling extension [{e.Name}]");
                            extobj.SetName(tokenname);
                            TokenParser.AnonymousTokens.Add(extobj);
                            value = value.Replace(obj + "<-" + strip[1], tokenname);
                        }
                    }
                }
            }
            return value;
        }
        private string EvaluateVar(string value)
        {
            //get the var scope
            List<Token> varList = null;
            if (value.Contains("$var%"))
                varList = TokenParser.GlobalVariables;
            else if (value.Contains("var%"))
                varList = _reference.LocalVariables;
            if (varList == null)
                Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.SyntaxException,
                    $"[244]Unexpected error occured.",Value));
            //assign based on operator

            var strip = value.Replace("$", "").Replace("var%", "");
            string[] assign = default(string[]);
            if (strip.Contains("++"))
                assign = strip.Split(new string[] { "++" }, StringSplitOptions.None);
            else if (strip.Contains("--"))
                assign = strip.Split(new string[] { "--" }, StringSplitOptions.None);
            else if (strip.Contains("+="))
                assign = strip.Split(new string[] { "+=" }, StringSplitOptions.None);
            else if (strip.Contains("-="))
                assign = strip.Split(new string[] { "-=" }, StringSplitOptions.None);
            else if (strip.Contains("="))
                assign = strip.Split(new string[] { "=" }, StringSplitOptions.None);

            //get the left hand
            var leftHand = assign[0].Replace(" ", "");
            var varRef = varList.FirstOrDefault(f => f.Name == leftHand);
            if (varRef != null && varRef.Locked)
                Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.SyntaxException,
                    $"[282]Cannot re-assign a sealed variable!", Value));
            //one sided assignment
            if (strip.Contains("++") || strip.Contains("--"))
            {
                if (varRef == null)
                    Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.SyntaxException,
                        $"[269]Cannot find the left hand variable.", Value));
                double numOut = 0;
                double.TryParse(varRef.ToString(), out numOut);
                if (strip.Contains("++"))
                    numOut++;
                else
                    numOut--;
                varRef.SetValue(numOut.ToString());
                return "";
            }
            Token token = null;
            var rightHand = assign[1].Replace(" ", "");
            //if (rightHand.Contains('+'))
            //{
                var parts = rightHand.Split('+');
                string output = "";
            foreach (var p in parts)
            {
                var x = p;
                if (x.Contains("->"))
                {
                    var fext = ParseExtensions(x);
                    var fcheckSplit = x.Split(new string[] { "->" }, StringSplitOptions.None);
                    var fcheck = TokenParser.FunctionList.FirstOrDefault(f => f.Name == fcheckSplit[0]);
                    if (fcheck != null)
                        x = ParseFunctions(x, fext);
                }
                if (x.Contains("<-"))
                    x = EvaluateVarExtensions(x);
                if (x == null || x == "" || x == " ")
                    Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.SyntaxException,
                        $"[688]Right hand must be a value.", Value));
                var ntoken = GetTokens(new string[] { x }).ElementAtOrDefault(0);
                if (ntoken == null)
                    Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.SyntaxException,
                        $"[692]Right hand must be a value.", Value));
                output += ntoken.ToString();
            }
                token = new Token("concatination", output, Value);
            rightHand = output;
            //}
            if (token == null)
                Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.SyntaxException,
                    $"[699]Right hand must be a value.", Value));
           
            if (strip.Contains("+=") || strip.Contains("-="))
            {
                if (varRef == null)
                    Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.SyntaxException,
                        $"[291]Cannot find the left hand variable.", Value));
                //check if number and apply the change
                double leftNumOut = 0;
                double rightNumOut = 0;

                //check if token is a number too
                var nofailRight = double.TryParse(token.ToString(), out rightNumOut);
                var nofailLeft = double.TryParse(varRef.ToString(), out leftNumOut);
                if (nofailLeft && nofailRight)
                {
                    if (strip.Contains("+="))
                        leftNumOut += rightNumOut;
                    else
                        leftNumOut -= rightNumOut;
                    varRef.SetValue(leftNumOut.ToString());
                }
                else//one or both arent numbers, which means concatenation intead of incrementation.
                {
                    var str = varRef.ToString();
                    if (strip.Contains("+="))
                        str += token.ToString();
                    else
                        Compiler.ExceptionListener.Throw(new ExceptionHandler(ExceptionType.SyntaxException,
                            "[314]Cannot apply the operand -= with type string.", Value));
                    varRef.SetValue(str);
                }
                return "";
            }
            if (strip.Contains("="))
            {
                if (varRef != null)
                {
                    varRef.SetValue(token.ToString());
                }
                else
                    varList.Add(new Token(leftHand, token.ToString(),Value));
                return "";
            }
            Compiler.ExceptionListener.Throw("[330]Unknown error with assignment.", ExceptionType.SyntaxException, Value);
            return "";
        }

        private Token Parse(TFunction t)
        {
            if (!_reference.ReturnFlag)
            {
                if (!TokenParser.Stop)
                {
                    if (_reference.Tracer == null || (!_reference.Tracer.Continue && !_reference.Tracer.Break))
                        return TryParseMember(t);
                }
                else if (TokenParser.Stop && _reference.BlindExecute)
                {
                    return TryParseMember(t);
                }
            }
            return null;
        }
        private Token TryParseMember(TFunction t)
        {
            if (t == null)
                return null;
            if (_reference.BlindExecute)
                t.BlindExecute = true;
            if (t.Name == "Base")
            {
                var b = _reference.Base;
                b.Extensions = new List<EDefinition>();
                if (t.Extensions != null)
                    b.Extensions = t.Extensions;
                if (t.Function.BlindExecute)
                    b.BlindExecute = true;
                /* ommitting this with the change to nested scope and multi parameter overrides
                 * 
                ///This is the whitelist for passing extensions to the Base function
                ///
                if (_reference.Extensions != null)
                {
                    foreach (var x in _reference.Extensions)
                    {
                        if (x.Name == "Concat" ||
                            x.Name == "Color" ||
                            x.Name == "Threshold")
                            b.Extensions.Add(x);
                    }
                }
                */
                b.TryParse(t);
                return b.ReturnBubble;
            }
            //change this plz

            var z = t.Function;
            if (t.Extensions != null)
            {
                z.Extensions = t.Extensions;
            }

            z.TryParse(t);
            return z.ReturnBubble;
        }
    }
    public static class StringExtensionMethods
    {
        public static string ReplaceFirst(this string text, string search, string replace)
        {
            int pos = text.IndexOf(search);
            if (pos < 0)
            {
                return text;
            }
            return text.Substring(0, pos) + replace + text.Substring(pos + search.Length);
        }
        public static string CleanString(this string input)
        {
            return input.Replace("&comma;", ",")
                .Replace("&plus;", "+")
                .Replace("&neg;", "-")
                .Replace("&eq;", "=")
                .Replace("&per;", "%")
                .Replace("&dollar;", "$")
                .Replace("&expl;", "!")
                .Replace("&lparen;", "(")
                .Replace("&rparen;", ")")
                .Replace("&lbrack;", "[")
                .Replace("&rbrack;", "]")
                .Replace("&lbrace;", "{")
                .Replace("&rbrace;", "}")
                .Replace("&lchev;", "<")
                .Replace("&rchev;", ">")
                .Replace("&period;", ".")
                .Replace("&amp;", "&");
        }
    }
}
