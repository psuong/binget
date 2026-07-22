using System.Text;

namespace BinGet.Templates;

public interface ITemplate {
    public void Format(StringBuilder stringBuilder, string path);
}
