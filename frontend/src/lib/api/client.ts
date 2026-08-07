import type { ApiError } from "@/types/api";
import { API_BASE_URL } from "@/lib/constants/config";

/**
 * Thin wrapper around fetch. Every non-2xx response is normalized into
 * ApiError and thrown ,  see the "Error response shape" in
 * docs/TECHNICAL_SPEC.md §14. Python stderr (surfaced by the backend in
 * `detail` for PythonExecutionException) flows through verbatim; callers
 * (toasts) must not genericize it.
 */
export class ApiClient {
  private baseUrl: string;

  constructor(baseUrl: string) {
    this.baseUrl = baseUrl.replace(/\/+$/, "");
  }

  async get<T>(
    path: string,
    params?: Record<string, string | number | boolean>
  ): Promise<T> {
    const res = await fetch(this.buildUrl(path, params), {
      method: "GET",
      headers: { Accept: "application/json" },
    });
    return this.handleResponse<T>(res);
  }

  async post<T>(path: string, body?: unknown): Promise<T> {
    const res = await fetch(this.buildUrl(path), {
      method: "POST",
      headers: { "Content-Type": "application/json", Accept: "application/json" },
      body: body !== undefined ? JSON.stringify(body) : undefined,
    });
    return this.handleResponse<T>(res);
  }

  async patch<T>(path: string, body?: unknown): Promise<T> {
    const res = await fetch(this.buildUrl(path), {
      method: "PATCH",
      headers: { "Content-Type": "application/json", Accept: "application/json" },
      body: body !== undefined ? JSON.stringify(body) : undefined,
    });
    return this.handleResponse<T>(res);
  }

  async delete<T>(path: string, params?: Record<string, string>): Promise<T> {
    const res = await fetch(this.buildUrl(path, params), {
      method: "DELETE",
      headers: { Accept: "application/json" },
    });
    return this.handleResponse<T>(res);
  }

  async postFormData<T>(
    path: string,
    formData: FormData,
    onProgress?: (progress: number) => void
  ): Promise<T> {
    // XHR, not fetch, because fetch has no upload-progress event ,  and
    // FileUploadZone needs real progress for the 100GB+ files described in
    // docs/PROJECT_PLAN.md §6 step 1.
    return new Promise<T>((resolve, reject) => {
      const xhr = new XMLHttpRequest();
      xhr.open("POST", this.buildUrl(path));
      xhr.responseType = "text";

      if (onProgress) {
        xhr.upload.onprogress = (evt) => {
          if (evt.lengthComputable) {
            onProgress(Math.round((evt.loaded / evt.total) * 100));
          }
        };
      }

      xhr.onload = () => {
        const status = xhr.status;
        let parsed: unknown = undefined;
        try {
          parsed = xhr.responseText ? JSON.parse(xhr.responseText) : undefined;
        } catch {
          // non-JSON body; leave parsed undefined
        }
        if (status >= 200 && status < 300) {
          resolve(parsed as T);
        } else {
          const err = parsed as Partial<ApiError> | undefined;
          reject({
            status,
            message: err?.message ?? xhr.statusText ?? "Request failed",
            detail: err?.detail,
          } satisfies ApiError);
        }
      };

      xhr.onerror = () => {
        reject({
          status: 0,
          message: "Network error",
          detail: `Could not reach ${this.baseUrl}`,
        } satisfies ApiError);
      };

      xhr.send(formData);
    });
  }

  async getBlob(path: string): Promise<Blob> {
    const res = await fetch(this.buildUrl(path));
    if (!res.ok) {
      throw await this.toApiError(res);
    }
    return res.blob();
  }

  private async handleResponse<T>(response: Response): Promise<T> {
    if (response.status === 204) {
      return undefined as T;
    }
    if (!response.ok) {
      throw await this.toApiError(response);
    }
    const text = await response.text();
    if (!text) return undefined as T;
    return JSON.parse(text) as T;
  }

  private async toApiError(response: Response): Promise<ApiError> {
    let body: Partial<ApiError> | undefined;
    try {
      body = await response.json();
    } catch {
      // body wasn't JSON ,  fall back to statusText below
    }
    return {
      status: response.status,
      message: body?.message ?? response.statusText ?? "Request failed",
      detail: body?.detail,
    };
  }

  private buildUrl(path: string, params?: Record<string, unknown>): string {
    const url = new URL(
      path.startsWith("/") ? path : `/${path}`,
      this.baseUrl
    );
    if (params) {
      for (const [key, value] of Object.entries(params)) {
        if (value !== undefined && value !== null) {
          url.searchParams.set(key, String(value));
        }
      }
    }
    return url.toString();
  }
}

export const apiClient = new ApiClient(API_BASE_URL);

export function isApiError(error: unknown): error is ApiError {
  return (
    typeof error === "object" &&
    error !== null &&
    "status" in error &&
    "message" in error
  );
}
