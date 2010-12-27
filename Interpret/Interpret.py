class Reader:
    def Read(self, Location): pass
    def End(self): pass

class StringReader(Reader):
    String = None
    def __init__(self, String):
        self.String = String
    def Read(self, Location):
        return self.String[Location]
    def End(self):
        return len(self.String)

def AcceptString(Reader, Location, String):
    if Reader.End() - Location < len(String):
        return False
    for t in range(0, len(String)):
        if not Reader.Read(Location + t) == String[t]:
            return False
    return (True, Location + len(String))

def IsWordChar(Char):
    ascii = ord(Char)
    if ascii >= 95 and ascii <= 122: return True  # _ ` Lowercase
    if ascii >= 65 and ascii <= 90: return True # Capitals
    if ascii >= 48 and ascii <= 58: return True # Numerals :
    if ascii == 39: return True # '
    if ascii == 126: return True # ~
    return False

def IsDigit(Char):
    return Char.isdigit()

Keywords = set([
    "function",
    "return",
    "struct",
    "variant",
    "if",
    "while",
    "else",
    "const"])

def IsWord(String):
    for t in range(1, len(String)):
        char = String[t]
        if (not IsDigit(char)) and not IsWordChar(char):
            return False
    return len(String) > 0 and not IsDigit(String[0]) and not (String in Keywords)

def AcceptDelimited(Reader, Location, DelimiterPattern, PartPattern):
    li = []
    sr = PartPattern(Reader, Location)
    if sr:
        first, Location = sr
        li.append(first)
        while True:
            sr = DelimiterPattern(Reader, Location)
            if sr:
                _, nlocation = sr
                sr = PartPattern(Reader, nlocation)
                if sr:
                    pat, Location = sr
                    li.append(pat)
                    continue
            break
        return li, Location
    return [], Location

def AcceptPadded(Reader, Location, Pattern):
    _, nlocation = AcceptWhitespace(Reader, Location)
    sr = Pattern(Reader, nlocation)
    if sr:
        res, nlocation = sr
        _, Location = AcceptWhitespace(Reader, nlocation)
        return res, Location

def AcceptSpaceCommaSpace(Reader, Location):
    return AcceptPadded(Reader, Location, lambda reader, location: AcceptString(reader, location, ","))

def AcceptWord(Reader, Location):
    word = ""
    first = True
    while Location < Reader.End():
        char = Reader.Read(Location)
        if first:
            if IsDigit(char) or not IsWordChar(char):
                return False
            first = False
        else:
            if (not IsDigit(char)) and (not IsWordChar(char)):
                break
        Location = Location + 1
        word = word + char
    if not first and not (word in Keywords):
        return (word, Location)

def AcceptWhitespace(Reader, Location):
    singlelinecomment = False
    multilinecomment = False
    while Location < Reader.End():
        char = Reader.Read(Location)
        if char == ' ' or char == '\t':
            Location = Location + 1
            continue
        if char == '\n' or char == '\t':
            Location = Location + 1
            singlelinecomment = False
            continue
        if multilinecomment:
            sr = AcceptString(Reader, Location, "*/")
            if sr:
                _, Location = sr
                multilinecomment = False
                continue
        if not (singlelinecomment or multilinecomment):
            sr = AcceptString(Reader, Location, "//")
            if sr:
                _, Location = sr
                singlelinecomment = True
                continue
            sr = AcceptString(Reader, Location, "/*")
            if sr:
                _, Location = sr
                multilinecomment = True
                continue
            return True, Location
        Location = Location + 1
    return True, Location

class Variant:
    FormTypes = None
    FormsByName = None
    def __init__(self, FormTypes, FormsByName):
        self.FormTypes = FormTypes
        self.FormsByName = FormsByName

class VariantValue:
    Type = None
    Form = None
    Data = None
    def __init__(self, Type, Form, Data):
        self.Type = Type
        self.Form = Form
        self.Data = Data

def MakeMaybeVariant(InnerType):
    return Variant([None, InnerType], {"nothing" : 0, "just" : 1})



class Expression:
    def Clone(self): pass
    def Call(self, Variables): pass
    def Substitute(self, Variables): return self

class VariableExpression(Expression):
    VarName = None
    def __init__(self, VarName):
        self.VarName = VarName
    def Clone(self):
        return VariableExpression(self.VarName)
    def Call(self, Variables):
        return Variables[self.VarName].Call(dict())
    def Substitute(self, Variables):
        try:
            return Variables[self.VarName]
        except:
            return self

class LiteralExpression(Expression):
    Value = None
    def __init__(self, Value):
        self.Value = Value
    def Clone(self):
        return LiteralExpression(self.Value)
    def Call(self, Variables):
        return self.Value

class ProcedureExpression(Expression):
    Statement = None
    def __init__(self, Statement):
        self.Statement = Statement
    def Clone(self):
        return ProcedureExpression(self.Statement.Clone())
    def Call(self, Variables):
        return self.Statement.Call(self, Variables)
    def Substitute(self, Variables):
        self.Statement = self.Statement.Substitute(Variables)
        return self

class FunctionCallExpression(Expression):
    Function = None
    Argument = None
    def __init__(self, Function, Argument):
        self.Function = Function
        self.Argument = Argument
    def Clone(self):
        return FunctionCallExpression(self.Function.Clone(), self.Argument.Clone())
    def Call(self, Variables):
        return self.Function.Call(Variables)(self.Argument.Call(Variables))
    def Substitute(self, Variables):
        self.Function = self.Function.Substitute(Variables)
        self.Argument = self.Argument.Substitute(Variables)
        return self

class FunctionDefineExpression(Expression):
    Function = None
    ArgumentType = None
    MapVarsFunc = None
    def __init__(self, Function, ArgumentType, MapVarsFunc):
        self.Function = Function
        self.ArgumentType = ArgumentType
        self.MapVarsFunc = MapVarsFunc
    def Clone(self):
        return FunctionDefineExpression(self.Function.Clone(), self.ArgumentType, self.MapVarsFunc)
    def Call(self, Variables):
        nvars = Variables.copy()
        def FuncCall(Argument):
            self.MapVarsFunc(Argument, nvars)
            return self.Function.Call(nvars)
        return FuncCall
    def Substitute(self, Variables):
        self.Function = self.Function.Substitute(Variables)
        return self

class AccessorExpression(Expression):
    Object = None
    Property = None
    def __init__(self, Object, Property):
        self.Object = Object
        self.Property = Property
    def Clone(self):
        return AccessorExpression(self.Object.Clone(), self.Property)
    def Call(self, Variables):
        objres = self.Object.Call(Variables)
        if objres.__class__ == Variant:
            return lambda arg: VariantValue(objres, objres.FormsByName[self.Property], arg)
        return Variables[self.Property](objres)
    def Substitute(self, Variables):
        self.Object = self.Object.Substitute(Variables)
        return self

class TupleExpression(Expression):
    Items = None
    def __init__(self, Items):
        self.Items = Items
    def Clone(self):
        return TupleExpression([x.Clone() for x in self.Items])
    def Call(self, Variables):
        return [x.Call(Variables) for x in self.Items]
    def Substitute(self, Variables):
        for i in range(0, len(self.Items)):
            self.Items[i] = self.Items[i].Substitute(Variables)
        return self

class VariantExpression(Expression):
    FormsByName = None
    FormTypes = None
    def __init__(self, FormTypes, FormsByName):
        self.FormTypes = FormTypes
        self.FormsByName = FormsByName
    def Clone(self):
        return VariantExpression([x.Clone() for x in self.FormTypes], self.FormsByName)
    def Call(self, Variables):
        return Variant([x.Call(Variables) for x in self.FormTypes], self.FormsByName)
    def Substitute(self, Variables):
        for i in range(0, len(self.FormTypes)):
            self.FormTypes[i] = self.FormTypes[i].Substitute(Variables)
        return self

def MakeLambda(ArgumentList, Expression):
    def MapVarsFunc(Argument, Variables):
        if type(Argument) == tuple or type(Argument) == list:
            for i in range(0, len(ArgumentList)):
                _, varname = ArgumentList[i]
                if varname:
                    Variables[varname] = LiteralExpression(Argument[i])
        else:
            Variables[ArgumentList[0][1]] = LiteralExpression(Argument)
    if len(ArgumentList) == 1:
        return FunctionDefineExpression(Expression, ArgumentList[0][0], MapVarsFunc)
    return FunctionDefineExpression(Expression, TupleExpression([x[0] for x in ArgumentList]), MapVarsFunc)

def AcceptIntegerLiteral(Reader, Location):
    intstr = ""
    while Location < Reader.End():
        char = Reader.Read(Location)
        if not IsDigit(char):
            break
        intstr = intstr + char
        Location = Location + 1
    if len(intstr) > 0:
        return int(intstr), Location
    else:
        return False

EscapeChars = {
    '\\' : "\\",
    'n' : "\n",
    'r' : "\r",
    't' : "\t",
    '\"' : "\""
}

def AcceptStringLiteral(Reader, Location):
    if Location < Reader.End():
        schar = Reader.Read(Location)
        if schar == "\"" or schar == "'":
            Location = Location + 1
            inescape = False
            string = ""
            while Location < Reader.End():
                char = Reader.Read(Location)
                if inescape:
                    string = string + EscapeChars[char]
                    inescape = False
                else:
                    if char == schar:
                        Location = Location + 1
                        break
                    if char == "\\":
                        inescape = True
                    else:
                        string = string + char
                Location = Location + 1
            return string, Location

def AcceptInnerVariant(Reader, Location):
    def AcceptVariantForm(Reader, Location):
        sr = AcceptWord(Reader, Location)
        if sr:
            formname, Location = sr
            _, nlocation = AcceptWhitespace(Reader, Location)
            sr = AcceptString(Reader, nlocation, "(")
            if sr:
                _, nlocation = sr
                sr = AcceptPadded(Reader, nlocation, lambda reader, location: AcceptExpressionList(reader, location))
                if sr:
                    argtypes, nlocation = sr
                    sr = AcceptString(Reader, nlocation, ")")
                    if sr:
                        _, Location = sr
                        if len(argtypes) == 1:
                            return (formname, argtypes[0]), Location
                        else:
                            return (formname, TupleExpression(argtypes)), Location
            return (formname, None), Location
    forms, Location = AcceptDelimited(Reader, Location, AcceptSpaceCommaSpace, AcceptVariantForm)
    formtypes = [x[1] for x in forms]
    formsbyname = dict()
    i = 0
    for form in forms:
        formsbyname[form[0]] = i
        i = i + 1
    return VariantExpression(formtypes, formsbyname), Location

def AcceptAtomExpression(Reader, Location):
    # Brackets
    sr = AcceptString(Reader, Location, "(")
    if sr:
        _, nlocation = sr
        _, nlocation = AcceptWhitespace(Reader, nlocation)
        arglist, nlocation = AcceptExpressionList(Reader, nlocation)
        sr = AcceptString(Reader, nlocation, ")")
        if sr:
            _, Location = sr
            if len(arglist) == 1:
                return arglist[0], Location
            else:
                return TupleExpression(arglist), Location
            
    # Single variable
    sr = AcceptWord(Reader, Location)
    if sr:
        varname, Location = sr
        return VariableExpression(varname), Location

    # Procedure
    sr = AcceptString(Reader, Location, "{")
    if sr:
        _, nlocation = sr
        _, nlocation = AcceptWhitespace(Reader, nlocation)
        proc, nlocation = AcceptCompoundStatement(Reader, nlocation)
        _, nlocation = AcceptWhitespace(Reader, nlocation)
        sr = AcceptString(Reader, nlocation, "}")
        if sr:
            _, Location = sr
            return proc, Location

    # Combined lambda/procedure
    sr = AcceptString(Reader, Location, "function")
    if sr:
        _, nlocation = sr
        _, nlocation = AcceptWhitespace(Reader, nlocation)
        sr = AcceptString(Reader, nlocation, "(")
        if sr:
            _, nlocation = sr
            _, nlocation = AcceptWhitespace(Reader, nlocation)
            arglist, nlocation = AcceptArgumentDefinitionList(Reader, nlocation)
            sr = AcceptString(Reader, nlocation, ")")
            if sr:
                _, nlocation = sr
                _, nlocation = AcceptWhitespace(Reader, nlocation)
                sr = AcceptString(Reader, nlocation, "{")
                if sr:
                    _, nlocation = sr
                    _, nlocation = AcceptWhitespace(Reader, nlocation)
                    proc, nlocation = AcceptCompoundStatement(Reader, nlocation)
                    _, nlocation = AcceptWhitespace(Reader, nlocation)
                    sr = AcceptString(Reader, nlocation, "}")
                    if sr:
                        _, Location = sr
                        return MakeLambda(arglist, proc), Location

    # Variant type definition
    sr = AcceptString(Reader, Location, "variant")
    if sr:
        _, nlocation = sr
        _, nlocation = AcceptWhitespace(Reader, nlocation)
        sr = AcceptString(Reader, nlocation, "{")
        if sr:
            _, nlocation = sr
            sr = AcceptPadded(Reader, nlocation, AcceptInnerVariant)
            if sr:
                variant, nlocation = sr
                _, nlocation = AcceptWhitespace(Reader, nlocation)
                sr = AcceptString(Reader, nlocation, "}")
                if sr:
                    _, Location = sr
                    return variant, Location

    # Integer literal
    sr = AcceptIntegerLiteral(Reader, Location)
    if sr:
        val, Location = sr
        return LiteralExpression(val), Location

    # String literal
    sr = AcceptStringLiteral(Reader, Location)
    if sr:
        val, Location = sr
        return LiteralExpression(val), Location

def AcceptTightExpression(Reader, Location):
    # Standard lambda
    sr = AcceptString(Reader, Location, "(")
    if sr:
        _, nlocation = sr
        _, nlocation = AcceptWhitespace(Reader, nlocation)
        arglist, nlocation = AcceptArgumentDefinitionList(Reader, nlocation)
        sr = AcceptString(Reader, nlocation, ")")
        if sr:
            _, nlocation = sr
            _, nlocation = AcceptWhitespace(Reader, nlocation)
            sr = AcceptString(Reader, nlocation, "=>")
            if sr:
                _, nlocation = sr
                _, nlocation = AcceptWhitespace(Reader, nlocation)
                sr = AcceptExpression(Reader, nlocation)
                if sr:
                    iexp, Location = sr
                    return MakeLambda(arglist, iexp), Location

    # Function type lambda
    sr = AcceptString(Reader, Location, "<")
    if sr:
        _, nlocation = sr
        _, nlocation = AcceptWhitespace(Reader, nlocation)
        arglist, nlocation = AcceptArgumentDefinitionList(Reader, nlocation)
        sr = AcceptString(Reader, nlocation, ">")
        if sr:
            _, nlocation = sr
            _, nlocation = AcceptWhitespace(Reader, nlocation)
            sr = AcceptTightExpression(Reader, nlocation)
            if sr:
                returntype, Location = sr
                return MakeLambda(arglist, returntype), Location

    # Atom followed by a sequence of calls or accessors
    sr = AcceptAtomExpression(Reader, Location)
    if sr:
        exp, Location = sr
        while True:
            # Function call
            _, nlocation = AcceptWhitespace(Reader, Location)
            sr = AcceptString(Reader, nlocation, "(")
            if sr:
                _, nlocation = sr
                _, nlocation = AcceptWhitespace(Reader, nlocation)
                arglist, nlocation = AcceptExpressionList(Reader, nlocation)
                _, nlocation = AcceptWhitespace(Reader, nlocation)
                sr = AcceptString(Reader, nlocation, ")")
                if sr:
                    _, Location = sr
                    if len(arglist) == 1:
                        exp = FunctionCallExpression(exp, arglist[0])
                    else:
                        exp = FunctionCallExpression(exp, TupleExpression(arglist))
                    continue

            # Accessor
            sr = AcceptString(Reader, nlocation, ".")
            if sr:
                _, nlocation = sr
                _, nlocation = AcceptWhitespace(Reader, nlocation)
                sr = AcceptWord(Reader, nlocation)
                if sr:
                    prop, Location = sr
                    exp = AccessorExpression(exp, prop)
                    continue
            break
        return exp, Location

Operators = {
    "==" : (7, True, lambda arg: arg[0] == arg[1]),
    "<" : (8, True, lambda arg: arg[0] < arg[1]),
    ">" : (8, True, lambda arg: arg[0] > arg[1]),
    "<=" : (8, True, lambda arg: arg[0] <= arg[1]),
    ">=" : (8, True, lambda arg: arg[0] >= arg[1]),
    "+" : (9, True, lambda arg: arg[0] + arg[1]),
    "-" : (9, True, lambda arg: arg[0] - arg[1]),
    "++" : (9, True, lambda arg: arg[0] + arg[1]),
    "*" : (10, True, lambda arg: arg[0] * arg[1]),
    "/" : (10, True, lambda arg: arg[0] / arg[1]),
    "%" : (10, True, lambda arg: arg[0] % arg[1])
}
OperatorMaxLen = 0
for k, v in Operators.items():
    if len(k) > OperatorMaxLen:
        OperatorMaxLen = len(k)

def AcceptArgumentDefinitionList(Reader, Location):
    def AcceptArgumentDefinition(Reader, Location):
        sr = AcceptTightExpression(Reader, Location)
        if sr:
            ttype, nlocation = sr
            _, nlocation = AcceptWhitespace(Reader, nlocation)
            sr = AcceptWord(Reader, nlocation)
            if sr:
                name, Location = sr
                return (ttype, name), Location
            return (ttype, None), nlocation
    return AcceptDelimited(Reader, Location, AcceptSpaceCommaSpace, AcceptArgumentDefinition)

def AcceptExpressionList(Reader, Location):
    return AcceptDelimited(Reader, Location, AcceptSpaceCommaSpace, AcceptExpression)

def AcceptOperator(Reader, Location):
    i = 0
    opname = ""
    match = None
    while i < OperatorMaxLen and Location < Reader.End():
        char = Reader.Read(Location)
        opname = opname + char
        try:
            opstr, opassoc, opdef = Operators[opname]
            match = (opname, opstr, opassoc, opdef), Location + 1
        except(KeyError):
            pass
        Location = Location + 1
        i = i + 1
    return match
            
def AcceptExpression(Reader, Location):
    sr = AcceptTightExpression(Reader, Location)
    if sr:
        firstexp, Location = sr
        def Merge(OptreeInitial, Operator, Expression):
            opname, opstr, opassoc, opdef = Operator
            if type(OptreeInitial) == tuple:
                leftpart, rightpart, iop = OptreeInitial
                iopname, iopstr, iopassoc, iopdef = iop
                if iopstr < opstr or (iopstr == opstr and not opassoc):
                    return (leftpart, Merge(rightpart, Operator, Expression), iop)
            return (OptreeInitial, Expression, Operator)
        def Convert(Optree):
            if type(Optree) == tuple:
                leftpart, rightpart, iop = Optree
                iopname, iopstr, iopassoc, iopdef = iop
                return FunctionCallExpression(LiteralExpression(iopdef), TupleExpression([Convert(leftpart), Convert(rightpart)]))
            return Optree
        optree = firstexp
        while True:
            _, nlocation = AcceptWhitespace(Reader, Location)
            sr = AcceptOperator(Reader, nlocation)
            if sr:
                opdata, nlocation = sr
                _, nlocation = AcceptWhitespace(Reader, nlocation)
                opname, opstr, opassoc, opdef = opdata
                sr = AcceptTightExpression(Reader, nlocation)
                if sr:
                    exp, Location = sr
                    optree = Merge(optree, (opname, opstr, opassoc, opdef), exp)
                    continue
            break
        return Convert(optree), Location
    return False





class Statement:
    def Clone(self): pass
    def Call(self, Variables): pass
    def Substitute(self, Variables): pass

class DefineStatement(Statement):
    Type = None
    VarName = None
    Value = None
    def __init__(self, Type, VarName, Value):
        self.Type = Type
        self.VarName = VarName
        self.Value = Value
    def Clone(self):
        return DefineStatement(self.Type.Clone(), self.VarName, self.Value.Clone())
    def Call(self, Variables):
        Variables[self.VarName] = self.Value.Clone().Substitute(Variables)
    def Substitute(self, Variables):
        self.Type = self.Type.Substitute(Variables)
        return self

class AssignStatement(Statement):
    VarName = None
    Value = None
    def __init__(self, VarName, Value):
        self.VarName = VarName
        self.Value = Value
    def Clone(self):
        return AssignStatement(self.VarName, self.Value.Clone())
    def Call(self, Variables):
        Variables[self.VarName] = self.Value.Clone().Substitute(Variables)
    def Substitute(self, Variables):
        self.Value = self.Value.Substitute(Variables)
        return self

class ReturnStatement(Statement):
    Value = None
    def __init__(self, Value):
        self.Value = Value
    def Clone(self):
        return ReturnStatement(self.Value.Clone())
    def Call(self, Variables):
        return self.Value.Call(Variables)
    def Substitute(self, Variables):
        self.Value = self.Value.Substitute(Variables)
        return self

class CompoundStatement(Statement):
    SubStatements = None
    def __init__(self, SubStatements):
        self.SubStatements = SubStatements
    def Clone(self):
        return CompoundStatement([x.Clone() for x in self.SubStatements])
    def Call(self, Variables):
        for statement in self.SubStatements:
            res = statement.Call(Variables)
            if not res == None:
                return res
        return None
    def Substitute(self, Variables):
        for i in range(0, len(self.SubStatements)):
            self.SubStatements[i] = self.SubStatements[i].Substitute(Variables)
        return self

class IfStatement(Statement):
    Condition = None
    OnTrue = None
    OnFalse = None
    def __init__(self, Condition, OnTrue, OnFalse):
        self.Condition = Condition
        self.OnTrue = OnTrue
        self.OnFalse = OnFalse
    def Clone(self):
        return IfStatement(self.Condition.Clone(), self.OnTrue.Clone(), self.OnFalse.Clone())
    def Call(self, Variables):
        if self.Condition.Call(Variables):
            if self.OnTrue:
                return self.OnTrue.Call(Variables)
        else:
            if self.OnFalse:
                return self.OnFalse.Call(Variables)
    def Substitute(self, Variables):
        self.Condition = self.Condition.Substitute(Variables)
        self.OnTrue = self.OnTrue.Substitute(Variables)
        self.OnFalse = self.OnFalse.Substitute(Variables)
        return self

class BreakNotice:
    Depth = 0
    def __init__(self, Depth):
        self.Depth = Depth

class BreakStatement(Statement):
    Depth = 0
    def __init__(self, Depth):
        self.Depth = Depth
    def Clone(self, Variables):
        return self
    def Call(self, Variables):
        return BreakNotice(self.Depth)

class WhileStatement(Statement):
    Condition = None
    Inner = None
    def __init__(self, Condition, Inner):
        self.Condition = Condition
        self.Inner = Inner
    def Clone(self):
        return WhileStatement(self.Condition.Clone(), self.Inner.Clone())
    def Call(self, Variables):
        while self.Condition.Call(Variables):
            lres = self.Inner.Call(Variables)
            if lres.__class__ == BreakNotice:
                if lres.Depth > 1:
                    return BreakNotice(lres.Depth - 1)
                else:
                    return None
            if not lres == None:
                return lres
    def Substitute(self, Variables):
        self.Condition = self.Condition.Substitute(Variables)
        self.Inner = self.Inner.Substitute(Variables)

class FixStatement(Statement):
    Vars = None
    def __init__(self, Vars):
        self.Vars = Vars
    def Clone(self):
        return FixStatement([(n[0].Clone(), n[1], n[2].Clone()) for n in self.Vars])
    def Call(self, Variables):
        innervals = dict()
        for var in self.Vars:
            ttype, name, val = var
            innervals[name] = val.Clone().Substitute(Variables)
        for name, val in innervals.items():
            Variables[name] = val.Substitute(innervals)
    def Substitute(self, Variables):
        for n in self.Vars:
            try:
                Variables.pop(n[1])
            except:
                pass
        for i in range(0, len(self.Vars)):
            self.Vars[i][0] = self.Vars[i][0].Substitute(Variables)
            self.Vars[i][2] = self.Vars[i][2].Substitute(Variables)
        return self
    
def AcceptDefine(Reader, Location):
    sr = AcceptTightExpression(Reader, Location)
    if sr:
        ttype, nlocation = sr
        _, nlocation = AcceptWhitespace(Reader, nlocation)
        sr = AcceptWord(Reader, nlocation)
        if sr:
            varname, nlocation = sr
            _, nlocation = AcceptWhitespace(Reader, nlocation)
            
            sr = AcceptString(Reader, nlocation, "=")
            if sr:
                _, nlocation = sr
                _, nlocation = AcceptWhitespace(Reader, nlocation)
                sr = AcceptExpression(Reader, nlocation)
                if sr:
                    value, nlocation = sr
                    return (ttype, varname, value), nlocation
                    
def AcceptStatement(Reader, Location):
    
    # Monads would've cleared all this code up
    # Define
    sr = AcceptDefine(Reader, Location)
    if sr:
        define, nlocation = sr
        _, nlocation = AcceptWhitespace(Reader, nlocation)
        sr = AcceptString(Reader, nlocation, ";")
        if sr:
            _, nlocation = sr
            return DefineStatement(define[0], define[1], define[2]), nlocation

    # Fix
    sr = AcceptString(Reader, Location, "fix")
    if sr:
        _, nlocation = sr
        _, nlocation = AcceptWhitespace(Reader, nlocation)
        sr = AcceptString(Reader, nlocation, "{")
        if sr:
            _, nlocation = sr
            _, nlocation = AcceptWhitespace(Reader, nlocation)
            varlist, nlocation = AcceptDelimited(Reader, nlocation, AcceptSpaceCommaSpace, AcceptDefine)
            _, nlocation = AcceptWhitespace(Reader, nlocation)
            sr = AcceptString(Reader, nlocation, "}")
            if sr:
                _, Location = sr
                return FixStatement(varlist), Location

    # Return
    sr = AcceptString(Reader, Location, "return")
    if sr:
        _, nlocation = sr
        _, nlocation = AcceptWhitespace(Reader, nlocation)
        sr = AcceptExpression(Reader, nlocation)
        if sr:
            value, nlocation = sr
            _, Location = AcceptWhitespace(Reader, nlocation)
            sr = AcceptString(Reader, nlocation, ";")
            if sr:
                _, nlocation = sr
                return ReturnStatement(value), nlocation

    # Assign
    sr = AcceptWord(Reader, Location)
    if sr:
        varname, nlocation = sr
        _, nlocation = AcceptWhitespace(Reader, nlocation)
        sr = AcceptString(Reader, nlocation, "=")
        if sr:
            _, nlocation = sr
            _, nlocation = AcceptWhitespace(Reader, nlocation)
            sr = AcceptExpression(Reader, nlocation)
            if sr:
                value, nlocation = sr
                _, nlocation = AcceptWhitespace(Reader, nlocation)
                sr = AcceptString(Reader, nlocation, ";")
                if sr:
                    _, nlocation = sr
                    return AssignStatement(varname, value), nlocation

    # If
    sr = AcceptString(Reader, Location, "if")
    if sr:
        _, nlocation = sr
        _, nlocation = AcceptWhitespace(Reader, nlocation)
        sr = AcceptExpression(Reader, nlocation)
        if sr:
            cond, nlocation = sr
            _, nlocation = AcceptWhitespace(Reader, nlocation)
            sr = AcceptStatement(Reader, nlocation)
            if sr:
                ontrue, Location = sr
                _, nlocation = AcceptWhitespace(Reader, Location)
                sr = AcceptString(Reader, nlocation, "else")
                if sr:
                    _, nlocation = sr
                    _, nlocation = AcceptWhitespace(Reader, nlocation)
                    sr = AcceptStatement(Reader, nlocation)
                    if sr:
                        onfalse, Location = sr
                        return IfStatement(cond, ontrue, onfalse), Location
                return IfStatement(cond, ontrue, None), Location

    # While
    sr = AcceptString(Reader, Location, "while")
    if sr:
        _, nlocation = sr
        _, nlocation = AcceptWhitespace(Reader, nlocation)
        sr = AcceptExpression(Reader, nlocation)
        if sr:
            cond, nlocation = sr
            _, nlocation = AcceptWhitespace(Reader, nlocation)
            sr = AcceptStatement(Reader, nlocation)
            if sr:
                inner, Location = sr
                return WhileStatement(cond, inner), Location

    # Compound
    sr = AcceptString(Reader, Location, "{")
    if sr:
        _, nlocation = sr
        _, nlocation = AcceptWhitespace(Reader, nlocation)
        sr = AcceptCompoundStatement(Reader, nlocation)
        if sr:
            statement, nlocation = sr
            _, nlocation = AcceptWhitespace(Reader, nlocation)
            sr = AcceptString(Reader, nlocation, "}")
            if sr:
                _, nlocation = sr
                return statement, nlocation

def AcceptCompoundStatement(Reader, Location):
    sr = AcceptStatement(Reader, Location)
    if sr:
        firststatement, Location = sr
        li = [firststatement]
        while True:
            _, nlocation = AcceptWhitespace(Reader, Location)
            sr = AcceptStatement(Reader, nlocation)
            if sr:
                statement, Location = sr
                li.append(statement)
                continue
            else:
                break
        return CompoundStatement(li), Location
    else:
        return CompoundStatement([]), Location


class SyntaxException(BaseException):
    pass
    

DefaultVariables = {
    # Types
    "type" : type,
    "int" : int,
    "string" : str,
    "char" : chr,
    "bool" : bool,
    "maybe" : MakeMaybeVariant,
    "list" : (lambda arg: list),

    # Constants
    "true" : True,
    "false" : False,

    # Unary operations
    "negative" : (lambda arg: -arg),
    "not" : (lambda arg: not arg),

    # List / string
    "length" : (lambda arg: len(arg)),
    "sub" : (lambda aarg: lambda barg: aarg[barg[0]:barg[1]]),
    "empty" : (lambda arg: []),
    "element" : (lambda aarg: lambda barg: aarg[barg]),
    "append" : (lambda aarg: lambda barg: aarg + [barg])
}
DefaultVariables = { k : LiteralExpression(v) for k, v in DefaultVariables.items() }

def InterpretFile(File):
    f = open(File, 'r')
    s = f.read()
    sr = StringReader(s)
    _, nlocation = AcceptWhitespace(sr, 0)
    exp = AcceptCompoundStatement(StringReader(s), nlocation)
    _, nlocation = AcceptWhitespace(sr, exp[1])
    if nlocation == len(s):
        return exp[0].Call(DefaultVariables.copy())
    else:
        raise SyntaxException()

def EvaluateString(String):
    exp = AcceptExpression(StringReader(String), 0)
    return exp[0].Call(DefaultVariables.copy())
    
res = InterpretFile("test.salt")
print(res)
