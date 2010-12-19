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
    if ascii >= 94 and ascii <= 122: return True  # ^ _ ` Lowercase
    if ascii >= 65 and ascii <= 90: return True # Capitals
    if ascii >= 47 and ascii <= 58: return True # / Numerals :
    if ascii == 33: return True # !
    if ascii >= 35 and ascii <= 39: return True # # $ % & '
    if ascii >= 42 and ascii <= 43: return True # * +
    if ascii == 45: return True # -
    if ascii >= 60 and ascii <= 63: return True # < = > ?
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
    Forms = None
    FormsByName = None
    def __init__(self, FormTypes, FormNames):
        self.Forms = []
        self.FormsByName = dict()
        for i in range(0, len(FormNames)):
            name = FormNames[i]
            self.Forms.append((name, FormTypes[i]))
            self.FormsByName[name] = i

class VariantValue:
    Type = None
    Form = None
    Data = None
    def __init__(self, Type, Form, Data):
        self.Type = Type
        self.Form = Form
        self.Data = Data

def MakeMaybeVariant(InnerType):
    return Variant([None, InnerType], ["nothing", "just"])



class Expression:
    def Call(self, Variables): pass

class VariableExpression(Expression):
    VarName = None
    def __init__(self, VarName):
        self.VarName = VarName
    def Call(self, Variables):
        return Variables[self.VarName]

class LiteralExpression(Expression):
    Value = None
    def __init__(self, Value):
        self.Value = Value
    def Call(self, Variables):
        return self.Value

class ProcedureExpression(Expression):
    Statement = None
    def __init__(self, Statement):
        self.Statement = Statement
    def Call(self, Variables):
        return self.Statement.Call(self, Variables)

class FunctionCallExpression(Expression):
    Function = None
    Argument = None
    def __init__(self, Function, Argument):
        self.Function = Function
        self.Argument = Argument
    def Call(self, Variables):
        return self.Function.Call(Variables)(self.Argument.Call(Variables))

class FunctionDefineExpression(Expression):
    Function = None
    ArgumentType = None
    MapVarsFunc = None
    def __init__(self, Function, ArgumentType, MapVarsFunc):
        self.Function = Function
        self.ArgumentType = ArgumentType
        self.MapVarsFunc = MapVarsFunc
    def Call(self, Variables):
        nvars = Variables.copy()
        def FuncCall(Argument):
            self.MapVarsFunc(Argument, nvars)
            return self.Function.Call(nvars)
        return FuncCall

class TupleExpression(Expression):
    Items = None
    def __init__(self, Items):
        self.Items = Items
    def Call(self, Variables):
        return [x.Call(Variables) for x in self.Items]

def MakeLambda(ArgumentList, Expression):
    typelist, namelist = ArgumentList
    def MapVarsFunc(Argument, Variables):
        if type(Argument) == tuple or type(Argument) == list:
            for i in range(0, len(namelist)):
                Variables[namelist[i]] = Argument[i]
        else:
            Variables[namelist[0]] = Argument
    if len(typelist) == 1:
        return FunctionDefineExpression(Expression, typelist[0], MapVarsFunc)
    return FunctionDefineExpression(Expression, TupleExpression(typelist), MapVarsFunc)

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

def AcceptAtomExpression(Reader, Location):
    # Brackets
    sr = AcceptString(Reader, Location, "(")
    if sr:
        _, nlocation = sr
        _, nlocation = AcceptWhitespace(Reader, nlocation)
        sr = AcceptExpression(Reader, nlocation)
        if sr:
            iexp, nlocation = sr
            _, nlocation = AcceptWhitespace(Reader, nlocation)
            sr = AcceptString(Reader, nlocation, ")")
            if sr:
                _, Location = sr
                return iexp, Location

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
            arglist, nlocation = AcceptArgumentList(Reader, nlocation, True)
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
        arglist, nlocation = AcceptArgumentList(Reader, nlocation, True)
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
        arglist, nlocation = AcceptArgumentList(Reader, nlocation, True)
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
                arglist, nlocation = AcceptArgumentList(Reader, nlocation, False)
                _, nlocation = AcceptWhitespace(Reader, nlocation)
                sr = AcceptString(Reader, nlocation, ")")
                if sr:
                    _, Location = sr
                    if len(arglist) == 1:
                        exp = FunctionCallExpression(exp, arglist[0])
                    else:
                        exp = FunctionCallExpression(exp, TupleExpression(arglist))
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

def AcceptArgumentList(Reader, Location, WithNames):
    typeli = []
    nameli = []
    first = True
    nlocation = Location
    while True:
        if first:
            first = False
        else:
            _, nlocation = AcceptWhitespace(Reader, Location)
            sr = AcceptString(Reader, nlocation, ",")
            if not sr:
                break
            _, nlocation = sr
            _, nlocation = AcceptWhitespace(Reader, nlocation)
        sr = AcceptTightExpression(Reader, nlocation)
        if not sr:
            break
        ttype, Location = sr
        _, nlocation = AcceptWhitespace(Reader, Location)
        if WithNames:
            sr = AcceptWord(Reader, nlocation)
            if sr:
                varname, Location = sr
                nameli.append(varname)
            else:
                nameli.append(None)
        typeli.append(ttype)
    if WithNames:
        return (typeli, nameli), Location
    else:
        return typeli, Location
            
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
            sr = AcceptWord(Reader, nlocation)
            if sr:
                opname, nlocation = sr
                try:
                    opstr, opassoc, opdef = Operators[opname]
                    _, nlocation = AcceptWhitespace(Reader, nlocation)
                    sr = AcceptTightExpression(Reader, nlocation)
                    if sr:
                        exp, Location = sr
                        optree = Merge(optree, (opname, opstr, opassoc, opdef), exp)
                        continue
                except(KeyError):
                    break
            break
        return Convert(optree), Location
    return False





class Statement:
    def Call(self, Variables): pass

class DefineStatement(Statement):
    Type = None
    VarName = None
    Value = None
    def __init__(self, Type, VarName, Value):
        self.Type = Type
        self.VarName = VarName
        self.Value = Value
    def Call(self, Variables):
        Variables[self.VarName] = self.Value.Call(Variables)

class AssignStatement(Statement):
    VarName = None
    Value = None
    def __init__(self, VarName, Value):
        self.VarName = VarName
        self.Value = Value
    def Call(self, Variables):
        Variables[self.VarName] = self.Value.Call(Variables)

class ReturnStatement(Statement):
    Value = None
    def __init__(self, Value):
        self.Value = Value
    def Call(self, Variables):
        return self.Value.Call(Variables)

class CompoundStatement(Statement):
    SubStatements = None
    def __init__(self, SubStatements):
        self.SubStatements = SubStatements
    def Call(self, Variables):
        for statement in self.SubStatements:
            res = statement.Call(Variables)
            if not res == None:
                return res
        return None

class IfStatement(Statement):
    Condition = None
    OnTrue = None
    OnFalse = None
    def __init__(self, Condition, OnTrue, OnFalse):
        self.Condition = Condition
        self.OnTrue = OnTrue
        self.OnFalse = OnFalse
    def Call(self, Variables):
        if self.Condition.Call(Variables):
            if self.OnTrue:
                return self.OnTrue.Call(Variables)
        else:
            if self.OnFalse:
                return self.OnFalse.Call(Variables)

class BreakNotice:
    Depth = 0
    def __init__(self, Depth):
        self.Depth = Depth

class BreakStatement(Statement):
    Depth = 0
    def __init__(self, Depth):
        self.Depth = Depth
    def Call(self, Variables):
        return BreakNotice(self.Depth)

class WhileStatement(Statement):
    Condition = None
    Inner = None
    def __init__(self, Condition, Inner):
        self.Condition = Condition
        self.Inner = Inner
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

def AcceptStatement(Reader, Location):
    
    # Monads would've cleared all this code up
    # Define
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
                    _, nlocation = AcceptWhitespace(Reader, nlocation)
                    sr = AcceptString(Reader, nlocation, ";")
                    if sr:
                        _, nlocation = sr
                        return DefineStatement(ttype, varname, value), nlocation

            # Static function definition
            sr = AcceptString(Reader, nlocation, "(")
            if sr:
                _, nlocation = sr
                _, nlocation = AcceptWhitespace(Reader, nlocation)
                arglist, nlocation = AcceptArgumentList(Reader, nlocation, True)
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
                            return DefineStatement(MakeLambda(arglist, ttype), varname, MakeLambda(arglist, proc)), Location
                

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

DefaultVariables = {
    # Types
    "type" : type,
    "int" : int,
    "string" : str,
    "char" : chr,
    "bool" : bool,
    "maybe" : MakeMaybeVariant,

    # Constants
    "true" : True,
    "false" : False,

    # Unary operations
    "negative" : (lambda arg: -arg),
    "not" : (lambda arg: not arg)
}

def InterpretFile(File):
    f = open(File, 'r')
    s = f.read()
    exp = AcceptCompoundStatement(StringReader(s), 0)
    return exp[0].Call(DefaultVariables.copy())

def EvaluateString(String):
    exp = AcceptExpression(StringReader(String), 0)
    return exp[0].Call(DefaultVariables.copy())
    
res = InterpretFile("test.salt")
print(res)
