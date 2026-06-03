using Microsoft.CodeAnalysis;
using Get.PLShared;

namespace QuickMarkup.SourceGen;
using AST = QuickMarkup.AST.AST;

interface IQuickMarkupLocationProvider
{
    Location Fallback { get; }
    Location GetLocation(Position start, Position end);
    Location GetLocation(AST? node);
}
