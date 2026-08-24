using Coding.Infrastructure.AiUiGenerator;
using Xunit;

namespace Coding.UnitTests.AiUiGenerator;

public sealed class AiUiGeneratorPolicyTests
{
    [Fact]
    public void Required_file_set_includes_page_component_routing_and_visual_system()
    {
        Assert.Equal(new[]{"src/App.tsx","src/pages/DashboardPage.tsx","src/components/DashboardShell.tsx","src/styles.css"},AiUiGeneratorPolicy.Sections.Keys);
    }

    [Fact]
    public void Validation_rejects_an_unexpected_or_missing_file_set()
    {
        Assert.Throws<InvalidOperationException>(()=>AiUiGeneratorPolicy.ValidateFiles(new Dictionary<string,string>{{"src/App.tsx",new string('x',100)}}));
    }

    [Fact]
    public void Validation_accepts_bounded_multi_layer_output()
    {
        var files=new Dictionary<string,string>
        {
            ["src/App.tsx"]="import {DashboardPage} from './pages/DashboardPage'; export default function App(){ return <DashboardPage/>; }"+new string(' ',40),
            ["src/pages/DashboardPage.tsx"]="export function DashboardPage(){ return <main aria-label='Dashboard'>Ready</main>; }"+new string(' ',40),
            ["src/components/DashboardShell.tsx"]="export function DashboardShell(){ return <section aria-label='Shell'>Content</section>; }"+new string(' ',40),
            ["src/styles.css"]=".dashboard { display:grid; min-height:100vh; color:var(--text); }"+new string(' ',40)
        };
        AiUiGeneratorPolicy.ValidateFiles(files);
    }

    [Fact]
    public void Sample_records_require_explicit_approval()
    {
        Assert.Throws<InvalidOperationException>(()=>AiUiGeneratorPolicy.ValidateSampleDataBoundary(new[]{"const mockData = [{ id: 1 }];"},false));
        AiUiGeneratorPolicy.ValidateSampleDataBoundary(new[]{"const mockData = [{ id: 1 }];"},true);
    }
}
