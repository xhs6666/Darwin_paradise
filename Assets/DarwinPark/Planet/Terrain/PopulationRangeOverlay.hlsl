// ★ 新增：同群各圆先求并集，再填色或绘制并集外沿；内侧重叠边界不会成为线条，也不叠加透明度。
TEXTURE2D(_PopulationData);
float _PopulationCount;
float4 _PopulationStyle; // x显示模式(0关/1填色/2边界)，y不透明度，z边界角宽

float3 ApplyPopulationRange(float3 color,float3 positionOS)
{
    if(_PopulationStyle.x<.5||_PopulationCount<.5)return color;
    float3 direction=normalize(positionOS);
    float distance=10,group=-1,opacity=0;float3 tint=0;
    float4 paint=0;
    [loop] for(int i=0;i<(int)_PopulationCount;i++)
    {
        float4 circle=LOAD_TEXTURE2D(_PopulationData,int2(0,i));
        float4 data=LOAD_TEXTURE2D(_PopulationData,int2(1,i));
        if(data.w!=group&&group>=0)
        {
            // ★ 适配：同群填色和外沿都使用该群不透明度，重叠圆不重复累积。
            float alpha=opacity*(_PopulationStyle.x<1.5?(distance<=0?1:0):1-smoothstep(_PopulationStyle.z,_PopulationStyle.z+max(fwidth(distance),.00001),abs(distance)));
            if(alpha>paint.a)paint=float4(tint,alpha);
            distance=10;
        }
        group=data.w;tint=data.rgb;opacity=LOAD_TEXTURE2D(_PopulationData,int2(2,i)).r;
        // 圆边界等价于dot=cos(radius)；只在边界附近换算线宽，不需要每像素acos。
        distance=min(distance,(circle.w-dot(direction,circle.xyz))/max(sqrt(saturate(1-circle.w*circle.w)),.0001));
    }
    float alpha=opacity*(_PopulationStyle.x<1.5?(distance<=0?1:0):1-smoothstep(_PopulationStyle.z,_PopulationStyle.z+max(fwidth(distance),.00001),abs(distance)));
    if(alpha>paint.a)paint=float4(tint,alpha);
    return lerp(color,paint.rgb,paint.a);
}
