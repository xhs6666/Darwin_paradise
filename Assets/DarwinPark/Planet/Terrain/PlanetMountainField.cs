using System.Collections.Generic;
using UnityEngine;

namespace DarwinPark.Planet
{
    /// <summary>★ 新增：低频连续造山带，细分支只修饰山脊，避免整个大陆长满等高圆丘。</summary>
    public sealed class PlanetMountainField
    {
        private readonly FBMNoise _belt;
        private readonly FBMNoise _detail;
        private readonly PlanetTerrainConfig _config;
        private const int BinsPerAxis=10;
        private readonly List<Range>[] _ranges;
        private readonly struct Range
        {
            public readonly Vector3 center,axis,across;
            public readonly float length,width,weight,phase,peakCount;
            public Range(Vector3 center,Vector3 axis,float length,float width,float weight,float phase,float peakCount)
            {
                this.center=center;this.axis=axis;across=Vector3.Cross(center,axis);
                this.length=length;this.width=width;this.weight=weight;this.phase=phase;this.peakCount=peakCount;
            }
        }
        public PlanetMountainField(PlanetTerrainConfig config)
        {
            _config = config;
            _belt = new FBMNoise(new FBMConfig { frequency = config.mountainFrequency,
                octaves = config.mountainOctaves, warpStrength = config.mountainWarp }, config.seed + 500);
            _detail = new FBMNoise(new FBMConfig { frequency = config.mountainFrequency * 18f,
                octaves = 4, warpStrength = config.mountainWarp }, config.seed + 733);
            if(config.landforms is {enabled:true,version:>=6})_ranges=BuildRanges(config);
        }
        public float Sample(Vector3 direction)
        {
            if(_ranges!=null)return SampleRanges(direction);
            // ★ 修复（防Bug回归）：细节既侵蚀山顶也扭曲脊线，避免生成光滑的封闭“火山口”。
            float fine = _detail.Sample(direction);
            float distance = Mathf.Abs(_belt.Sample(direction) + fine * 0.06f);
            float belt = 1f - Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(_config.mountainCoreWidth, _config.mountainBeltWidth, distance));
            float ridge = Mathf.Pow(belt, _config.mountainSharpness);
            float detail = Mathf.Clamp01(1f - Mathf.Abs(fine) * 3f);
            return ridge * Mathf.Lerp(1f - _config.mountainDetailStrength, 1f, detail);
        }

        private static int Bin(float value)=>Mathf.Clamp((int)((value+1)*.5f*BinsPerAxis),0,BinsPerAxis-1);
        private static int Index(int x,int y,int z)=>x+BinsPerAxis*(y+BinsPerAxis*z);
        private static List<Range>[] BuildRanges(PlanetTerrainConfig config)
        {
            var random=new DeterministicRandom(unchecked(config.seed+9827));
            float Roll()=>random.NextFloat();
            var bins=new List<Range>[BinsPerAxis*BinsPerAxis*BinsPerAxis];
            int groups=Mathf.Clamp(Mathf.RoundToInt(config.mountainFrequency*32),28,90);
            for(int group=0;group<groups;group++)
            {
                float height=1-2*(group+.5f)/groups;
                float angle=group*2.399963f+Roll()*.48f+config.seed*.00017f;
                float ring=Mathf.Sqrt(1-height*height);
                var center=new Vector3(ring*Mathf.Cos(angle),height,ring*Mathf.Sin(angle));
                var axis=Vector3.Cross(center,Mathf.Abs(height)>.9f?Vector3.right:Vector3.up).normalized;
                axis=Quaternion.AngleAxis(Roll()*360,center)*axis;
                var side=Vector3.Cross(center,axis);
                int count=Roll()<.38f?1:Roll()<.65f?2:3;
                for(int i=0;i<count;i++)
                {
                    float offset=i==0?0:(i==1?1:-1)*(.052f+Roll()*.048f);
                    var ridgeCenter=(center+side*offset+axis*(i==0?0:(Roll()-.5f)*.09f)).normalized;
                    var ridgeAxis=Vector3.ProjectOnPlane(axis,ridgeCenter).normalized;
                    ridgeAxis=Quaternion.AngleAxis((Roll()-.5f)*65,ridgeCenter)*ridgeAxis;
                    float length=.038f+Roll()*.074f, width=.016f+Roll()*.032f;
                    var range=new Range(ridgeCenter,ridgeAxis,length,width,.55f+Roll()*.45f,Roll()*6.283185f,i==0&&count==1?1+Roll()*2:2+Roll()*3);
                    float reach=length+width*1.5f;
                    for(int z=Bin(ridgeCenter.z-reach);z<=Bin(ridgeCenter.z+reach);z++)
                    for(int y=Bin(ridgeCenter.y-reach);y<=Bin(ridgeCenter.y+reach);y++)
                    for(int x=Bin(ridgeCenter.x-reach);x<=Bin(ridgeCenter.x+reach);x++)
                        (bins[Index(x,y,z)]??=new List<Range>()).Add(range);
                }
            }
            return bins;
        }

        private float SampleRanges(Vector3 direction)
        {
            var bin=_ranges[Index(Bin(direction.x),Bin(direction.y),Bin(direction.z))];
            if(bin==null)return 0;
            float maximum=0;
            foreach(var range in bin)
            {
                if(Vector3.Dot(direction,range.center)<.96f)continue;
                float along=Vector3.Dot(direction,range.axis)/range.length;
                if(Mathf.Abs(along)>=1)continue;
                float cross=Vector3.Dot(direction,range.across)/range.width;
                cross-=Mathf.Sin(along*4+range.phase)*.27f;
                float footprint=1-along*along-cross*cross;
                if(footprint<=0)continue;
                float peaks=.5f+.5f*Mathf.Cos(along*range.peakCount*Mathf.PI+range.phase);
                float profile=Mathf.Pow(footprint,Mathf.Clamp(_config.mountainSharpness*.43f,.7f,2.4f));
                maximum=Mathf.Max(maximum,profile*range.weight*Mathf.Lerp(.40f,1,peaks));
            }
            if(maximum<=0)return 0;
            float detail=Mathf.Clamp01(1-Mathf.Abs(_detail.Sample(direction))*2.5f);
            return maximum*Mathf.Lerp(1-_config.mountainDetailStrength,1,detail);
        }
    }
}
