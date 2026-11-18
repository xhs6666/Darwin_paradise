using UnityEngine;

namespace DarwinPark.Planet
{
    // ★ 新增：在低分辨率上预计算季风与表层洋流。实体取样时只插值，不再追踪风程或海岸。
    public sealed class ClimateCirculation
    {
        private readonly int _width, _height;
        private readonly Vector3[] _onshore, _current;
        private readonly float[] _heat, _seasonalMoisture;

        internal ClimateCirculation(PlanetTerrainData terrain, PlanetClimateConfig config, int width)
        {
            _width = width;
            _height = width / 2;
            int count = _width * _height;
            _onshore = new Vector3[count];
            _current = new Vector3[count];
            _heat = new float[count];
            _seasonalMoisture = new float[count];
            var settings = config.earth;
            if (settings?.enabled != true) return;

            bool Ocean(Vector3 dir) => PlanetTerrainSampler.SampleHeight(terrain, dir) < terrain.SeaLevel;
            for (int y = 0; y < _height; y++)
            for (int x = 0; x < _width; x++)
            {
                int index = y * _width + x;
                var dir = PlanetTerrainSampler.DirectionFromUV((x + .5f) / _width, (y + .5f) / _height);
                var east = Vector3.Cross(Vector3.up, dir).normalized;
                var north = Vector3.Cross(dir, east);
                bool sea = Ocean(dir);
                var wind = EarthClimateRules.Wind(dir, config);
                var towardLand = Vector3.zero;
                int landContacts = 0;
                for (int side = 0; side < 8; side++)
                {
                    float angle = side * Mathf.PI / 4;
                    var heading = east * Mathf.Cos(angle) + north * Mathf.Sin(angle);
                    if (Ocean((dir + heading * settings.circulationCoastReach).normalized)) towardLand -= heading;
                    else { towardLand += heading; landContacts++; }
                }

                if (!sea && landContacts > 0 && landContacts < 8)
                {
                    float seasonalLatitude = Mathf.Clamp01((.72f - Mathf.Abs(dir.y)) / .3f);
                    float coastStrength = Mathf.Clamp01(towardLand.magnitude / 4);
                    _onshore[index] = towardLand.normalized * (seasonalLatitude * coastStrength);
                    _seasonalMoisture[index] = seasonalLatitude * coastStrength * settings.monsoonHumiditySwing;
                }

                // 受风驱动，南北半球向不同侧偏转。碰岸时沿岸转向，阻止流向陆地。
                var current = (wind + Vector3.Cross(dir, wind) * (-dir.y * settings.currentDeflection)).normalized;
                if (!Ocean((dir + current * settings.circulationCoastReach).normalized) && towardLand.sqrMagnitude > .001f)
                    current = Vector3.ProjectOnPlane(current, towardLand.normalized).normalized;

                if (!sea) continue;
                _current[index] = current * settings.surfaceCurrentSpeed;
                float sourceLatitude = Mathf.Abs((dir - current * settings.currentHeatReach).normalized.y);
                _heat[index] = Mathf.Clamp((Mathf.Abs(dir.y) - sourceLatitude) / Mathf.Max(.01f,settings.currentHeatReach), -1, 1);
            }
        }

        internal float Heat(float u,float v) => Read(_heat,u,v);
        internal float Monsoon(float u,float v,float northernSummer)
        {
            float hemisphere = Mathf.Clamp((v - .5f) * 16, -1, 1);
            return Read(_seasonalMoisture,u,v) * hemisphere * northernSummer;
        }

        public Vector3 SurfaceCurrent(Vector2 uv) => Read(_current,uv.x,uv.y);
        public Vector3 Wind(Vector3 direction,Vector2 uv,PlanetClimateConfig config,float northernSummer)
        {
            float summer = Mathf.Clamp((uv.y - .5f) * 16, -1, 1) * northernSummer;
            return (EarthClimateRules.Wind(direction,config) + Read(_onshore,uv.x,uv.y) *
                (summer * config.earth.monsoonWindStrength)).normalized;
        }

        private void Corners(float u,float v,out int a,out int b,out int c,out int d,out float tx,out float ty)
        {
            float x = Mathf.Repeat(u,1) * _width - .5f;
            float y = Mathf.Clamp(v * _height - .5f, 0, _height - 1);
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            int nextX = (x0+1+_width)%_width, nextY = Mathf.Min(y0+1,_height-1);
            int firstX = (x0+_width)%_width;
            a=y0*_width+firstX; b=y0*_width+nextX;
            c=nextY*_width+firstX; d=nextY*_width+nextX;
            tx=x-x0; ty=y-y0;
        }

        private float Read(float[] values,float u,float v)
        {
            Corners(u,v,out int a,out int b,out int c,out int d,out float x,out float y);
            return Mathf.Lerp(Mathf.Lerp(values[a],values[b],x),Mathf.Lerp(values[c],values[d],x),y);
        }

        private Vector3 Read(Vector3[] values,float u,float v)
        {
            Corners(u,v,out int a,out int b,out int c,out int d,out float x,out float y);
            return Vector3.Lerp(Vector3.Lerp(values[a],values[b],x),Vector3.Lerp(values[c],values[d],x),y);
        }
    }
}
