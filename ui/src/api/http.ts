import axios, { type AxiosError, type AxiosRequestConfig } from 'axios';

export type AccessTokenProvider = () => Promise<string | undefined>;

let accessTokenProvider: AccessTokenProvider | null = null;

export const setAccessTokenProvider = (provider: AccessTokenProvider | null) => {
  accessTokenProvider = provider;
};

export const httpClient = axios.create({ baseURL: import.meta.env.VITE_API_BASE_URL ?? '' });

httpClient.interceptors.request.use(async (config) => {
  const token = await accessTokenProvider?.();
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

export const http = <T>(config: AxiosRequestConfig, options?: AxiosRequestConfig): Promise<T> =>
  httpClient.request<T>({ ...config, ...options }).then(({ data }) => data);

export type ErrorType<Error> = AxiosError<Error>;
export type BodyType<Body> = Body;
