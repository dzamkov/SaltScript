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
    if ascii == 61: return True # =
    if ascii == 63: return True # ?
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





class Expression:
    def Call(Variables): pass

class VariableExpression:
    VarName = None
    def __init__(self, VarName):
        self.VarName = VarName

class LiteralExpression:
    Type = None
    Value = None
    def __init__(self, Type, Value):
        self.Type = Type
        self.Value = Value

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

def AcceptAtomExpression(Reader, Location):
    sr = AcceptWord(Reader, Location)
    if sr:
        varname, Location = sr
        return VariableExpression(varname), Location
    sr = AcceptIntegerLiteral(Reader, Location)
    if sr:
        val, Location = sr
        return LiteralExpression(int, val), Location

def AcceptTightExpression(Reader, Location):
    return AcceptAtomExpression(Reader, Location)

def AcceptExpression(Reader, Location):
    return AcceptTightExpression(Reader, Location)





class Statement:
    def Call(Variables): pass

class AssignStatement:
    Type = None
    VarName = None
    Value = None
    def __init__(self, Type, VarName, Value):
        self.Type = Type
        self.VarName = VarName
        self.Value = Value

def AcceptStatement(Reader, Location):
    
    # Monads would've cleared all this code up
    sr = AcceptTightExpression(Reader, Location)
    if sr:
        ttype, Location = sr
        _, Location = AcceptWhitespace(Reader, Location)
        sr = AcceptWord(Reader, Location)
        if sr:
            varname, Location = sr
            _, Location = AcceptWhitespace(Reader, Location)
            sr = AcceptString(Reader, Location, "=")
            if sr:
                _, Location = sr
                _, Location = AcceptWhitespace(Reader, Location)
                sr = AcceptExpression(Reader, Location)
                if sr:
                    value, Location = sr
                    _, Location = AcceptWhitespace(Reader, Location)
                    sr = AcceptString(Reader, Location, ";")
                    if sr:
                        _, Location = sr
                        return AssignStatement(ttype, varname, value), Location
    
    
test = AcceptStatement(StringReader("int test = 5; return test;"), 0)
print(test)
