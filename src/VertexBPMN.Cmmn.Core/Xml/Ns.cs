using System.Xml.Linq;

namespace VertexBPMN.Domain.Model.Cmmn.Xml;

internal static class Ns
{
    public static readonly XNamespace CMMN = "http://www.omg.org/spec/CMMN/20151109/MODEL";
    public static readonly XNamespace CMMNDI = "http://www.omg.org/spec/CMMN/20151109/CMMNDI";
    public static readonly XNamespace DI = "http://www.omg.org/spec/DD/20100524/DI";
    public static readonly XNamespace DC = "http://www.omg.org/spec/DD/20100524/DC";

    public static XName C(this string local) => CMMN + local;
    public static XName Cdi(this string local) => CMMNDI + local;
    public static XName Di(this string local) => DI + local;
    public static XName Dc(this string local) => DC + local;
}