using System.IO;
using System.Text;

namespace DarwinPark.Core
{
    // ★ 新增：外部配置允许中文行/块注释；逐字符识别字符串，避免把路径或URL里的斜杠当注释删掉。
    public static class CommentedJson
    {
        public static string Strip(string text)
        {
            var result=new StringBuilder(text.Length);bool quoted=false,escaped=false;
            for(int i=0;i<text.Length;i++)
            {
                char c=text[i];
                if(quoted){result.Append(c);if(escaped)escaped=false;else if(c=='\\')escaped=true;else if(c=='"')quoted=false;continue;}
                if(c=='"'){quoted=true;result.Append(c);continue;}
                if(c=='/'&&i+1<text.Length&&text[i+1]=='/')
                {while(i<text.Length&&text[i]!='\n'){result.Append(' ');i++;}if(i<text.Length)result.Append('\n');continue;}
                if(c=='/'&&i+1<text.Length&&text[i+1]=='*')
                {
                    result.Append("  ");i+=2;bool closed=false;
                    for(;i<text.Length;i++){if(text[i]=='*'&&i+1<text.Length&&text[i+1]=='/'){result.Append("  ");i++;closed=true;break;}result.Append(text[i]=='\n'?'\n':' ');}
                    if(!closed)throw new InvalidDataException("配置块注释缺少 */");continue;
                }
                result.Append(c);
            }
            return result.ToString();
        }
    }
}
