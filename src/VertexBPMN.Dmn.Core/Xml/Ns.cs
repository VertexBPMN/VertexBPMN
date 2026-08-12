using System.Xml.Linq;

namespace VertexBPMN.Domain.Model.Dmn.Xml;

internal static class Ns
{
    public static readonly XNamespace DMN = "https://www.omg.org/spec/DMN/20191111/MODEL/";
    public static readonly XNamespace DMNDI = "https://www.omg.org/spec/DMN/20191111/DMNDI/";
    public static readonly XNamespace DI = "http://www.omg.org/spec/DD/20100524/DI";
    public static readonly XNamespace DC = "http://www.omg.org/spec/DD/20100524/DC";

    public static XName N(this string local) => DMN + local;
    public static XName D(this string local) => DMNDI + local;
    public static XName Di(this string local) => DI + local;
    public static XName Dc(this string local) => DC + local;
}