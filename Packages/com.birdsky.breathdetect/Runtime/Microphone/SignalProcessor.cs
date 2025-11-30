using MathNet.Numerics.IntegralTransforms;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BreathDetect
{
    /// <summary>
    /// 能量计算（可注入不同算法），频谱分析 / 主频提取
    /// </summary>
    public class SignalProcessor
    {
        public static float CalculateRMS(float[] samples)
        {
            int sampleWindow = samples.Length;
            float sum = 0f;
            for (int i = 0; i < sampleWindow; i++)
            {
                sum += samples[i] * samples[i];
            }
            return Mathf.Sqrt(sum / sampleWindow);
        }
        public static float CalculateRMS_dB(float[] samples)
        {
            float rms = CalculateRMS(samples);
            return 20f * Mathf.Log10(rms);
        }
        public static float CalculateMainFrequency(float[] samples)
        {
            int sampleWindow = samples.Length;
            float maxMagnitude = 0f;
            int mainIndex = 0;

            MathNet.Numerics.Complex32[] complex = new MathNet.Numerics.Complex32[sampleWindow];
            for (int i = 0; i < complex.Length; i++)
            {
                complex[i] = new MathNet.Numerics.Complex32(samples[i], 0);
            }
            Fourier.Forward(complex);

            for (int i = 1; i < complex.Length - 1; i++)
            {
                if (complex[i].Real > maxMagnitude)
                {
                    maxMagnitude = complex[i].Real;
                    mainIndex = i;
                }
            }
            return mainIndex * MicrophoneDevice.sampleFrequency / (float)complex.Length;
        }
        public static float CalculateMaxValue(float[] samples)
        {
            float max = 0f;
            for (int i = 0; i < samples.Length; ++i)
            {
                max = Mathf.Max(max, Mathf.Abs(samples[i]));
            }
            return max;
        }
        public static void Normalize(ref float[] array, float value = 1f)
        {
            float max = CalculateMaxValue(array);
            if (max < Mathf.Epsilon) return;
            float r = value / max;
            for (int i = 0; i < array.Length; ++i)
            {
                array[i] *= r;
            }
        }
        public static float CosineSimilarity(float[] vector1, float[] vector2)
        {
            // 检查向量长度是否相同
            if (vector1.Length != vector2.Length)
            {
                return 0;
            }

            float dotProduct = 0f;
            float magnitude1 = 0f;
            float magnitude2 = 0f;

            for (int i = 0; i < vector1.Length; i++)
            {
                dotProduct += vector1[i] * vector2[i];
                magnitude1 += vector1[i] * vector1[i];
                magnitude2 += vector2[i] * vector2[i];
            }

            magnitude1 = Mathf.Sqrt(magnitude1);
            magnitude2 = Mathf.Sqrt(magnitude2);

            // 避免除以零
            if (magnitude1 < Mathf.Epsilon || magnitude2 < Mathf.Epsilon)
            {
                return 0f;
            }

            return dotProduct / (magnitude1 * magnitude2);
        }
        public static float[] CalculateSpectrum_FFT(float[] samples)
        {
            int sampleWindow = samples.Length;
            MathNet.Numerics.Complex32[] complex = new MathNet.Numerics.Complex32[sampleWindow];
            for (int i = 0; i < sampleWindow; ++i)
            {
                complex[i] = new MathNet.Numerics.Complex32(samples[i], 0);
            }
            Fourier.Forward(complex);

            float[] spectrum = new float[sampleWindow];
            for (int i = 0; i < sampleWindow; ++i)
            {
                spectrum[i] = complex[i].Magnitude;
            }
            return spectrum;
        }
        /// <summary>
        /// 低通滤波器
        /// </summary>
        /// <param name="data"></param>
        /// <param name="cutoff">截止频率</param>
        /// <param name="range">过渡带宽</param>
        public static void LowPassFilter(ref float[] data, float cutoff, float range)
        {
            float sampleRate = MicrophoneDevice.sampleFrequency;
            cutoff = (cutoff - range) / sampleRate;
            range /= sampleRate;

            float[] tmp = (float[])data.Clone();

            int n = (int)Mathf.Round(3.1f / range);
            if ((n + 1) % 2 == 0) n += 1;
            float[] b = new float[n];

            // 生成滤波器系数
            for (int i = 0; i < n; ++i)
            {
                float x = i - (n - 1) / 2f;
                float ang = 2f * Mathf.PI * cutoff * x;
                b[i] = 2f * cutoff * Mathf.Sin(ang) / ang;
            }

            // 应用滤波器
            for (int i = 0; i < data.Length; ++i)
            {
                data[i] = 0f; // 重置
                for (int j = 0; j < n; ++j)
                {
                    if (i - j >= 0)
                    {
                        data[i] += b[j] * tmp[i - j];
                    }
                }
            }
        }
        /// <summary>
        /// 预加重，一阶高通滤波器，增强高频成分 y[n] = x[n] - p * x[n-1]
        /// </summary>
        /// <param name="data"></param>
        /// <param name="p"></param>
        public static void PreEmphasis(ref float[] data, float p)
        {
            float[] tmp = (float[])data.Clone();
            for (int i = 1; i < data.Length; ++i)
            {
                data[i] = tmp[i] - p * tmp[i - 1];
            }
        }
        public static void HammingWindow(ref float[] array)
        {
            for (int i = 0; i < array.Length; ++i)
            {
                float x = (float)i / (array.Length - 1);
                array[i] *= 0.54f - 0.46f * Mathf.Cos(2f * Mathf.PI * x);
            }
        }
        /// <summary>
        /// 将线性频率刻度转换为梅尔频率刻度，模拟人耳听觉特性
        /// </summary>
        /// <param name="spectrum"></param>
        /// <param name="melDiv">维数</param>
        /// <returns></returns>
        public static float[] MelFilterBank(float[] spectrum, int melDiv)
        {
            float sampleRate = MicrophoneDevice.sampleFrequency;
            float[] melSpectrum = new float[melDiv];
            float fMax = sampleRate / 2;
            float melMax = ToMel(fMax);
            int nMax = spectrum.Length / 2;
            float df = fMax / nMax;
            float dMel = melMax / (melDiv + 1);

            for (int n = 0; n < melDiv; ++n)
            {
                float melBegin = dMel * n;
                float melCenter = dMel * (n + 1);
                float melEnd = dMel * (n + 2);

                float fBegin = ToHz(melBegin);
                float fCenter = ToHz(melCenter);
                float fEnd = ToHz(melEnd);

                int iBegin = (int)Mathf.Ceil(fBegin / df);
                int iCenter = (int)Mathf.Round(fCenter / df);
                int iEnd = (int)Mathf.Floor(fEnd / df);

                float sum = 0f;
                for (int i = iBegin + 1; i <= iEnd; ++i)
                {
                    float f = df * i;
                    float a = (i < iCenter) ?
                        (f - fBegin) / (fCenter - fBegin) :
                        (fEnd - f) / (fEnd - fCenter);
                    a /= (fEnd - fBegin) * 0.5f;
                    sum += a * spectrum[i];
                }
                melSpectrum[n] = sum;
            }
            return melSpectrum;
        }
        private static float ToMel(float hz, bool slaney = false)
        {
            float a = slaney ? 2595f : 1127f;
            return a * Mathf.Log(hz / 700f + 1f);
        }

        private static float ToHz(float mel, bool slaney = false)
        {
            float a = slaney ? 2595f : 1127f;
            return 700f * (Mathf.Exp(mel / a) - 1f);
        }
        public static float[] CalculateDCT(float[] spectrum)
        {
            int len = spectrum.Length;
            float[] cepstrum = new float[len];
            float a = Mathf.PI / len;

            for (int i = 0; i < len; ++i)
            {
                float sum = 0f;
                for (int j = 0; j < len; ++j)
                {
                    float ang = (j + 0.5f) * i * a;
                    sum += spectrum[j] * Mathf.Cos(ang);
                }
                cepstrum[i] = sum;
            }
            return cepstrum;
        }
        /*
        public float[] ExtractMFCC(float[] audioFrame, int sampleRate, int mfccCount)
        {
            // 1. 预加重
            SignalProcessor.PreEmphasis(ref audioFrame, 0.97f);
            // 2. 加窗
            SignalProcessor.HammingWindow(ref audioFrame);
            // 3. 零填充
            float[] paddedFrame = SignalProcessor.ZeroPadding(audioFrame);
            // 4. FFT得到频谱
            float[] spectrum = SignalProcessor.FFT(paddedFrame);
            // 5. 梅尔滤波器组
            float[] melSpectrum = SignalProcessor.MelFilterBank(spectrum, sampleRate, 26);
            // 6. 功率转分贝
            SignalProcessor.PowerToDb(ref melSpectrum);
            // 7. DCT得到MFCC
            float[] allMFCC = SignalProcessor.DCT(melSpectrum);
            // 8. 取前N个系数（去除DC分量）
            float[] mfcc = new float[mfccCount];
            Array.Copy(allMFCC, 1, mfcc, 0, mfccCount);

            return mfcc;
        }
        */
    }
}
