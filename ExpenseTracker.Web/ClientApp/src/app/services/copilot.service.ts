import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environment';

export interface ChatMessage {
  id: string;
  role: 'user' | 'assistant' | 'error';
  content: string;
  timestamp: Date;
}

export interface CopilotChatRequest {
  message: string;
  history?: Array<{ role: string; content: string }>;
}

export interface CopilotChatResponse {
  success: boolean;
  reply: string;
  error?: string;
}

@Injectable({
  providedIn: 'root',
})
export class CopilotService {
  private readonly endpoint = `${environment.api}/api/copilot/chat`;
  private readonly streamEndpoint = `${environment.api}/api/copilot/chat/stream`;

  constructor(private readonly http: HttpClient) {}

  chat(request: CopilotChatRequest): Observable<CopilotChatResponse> {
    return this.http.post<CopilotChatResponse>(this.endpoint, request);
  }

  async chatStream(
    request: CopilotChatRequest,
    onToken: (token: string) => void,
    onComplete?: (sanitizedReply: string) => void,
  ): Promise<void> {
    const response = await fetch(this.streamEndpoint, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    });

    if (!response.ok || !response.body) {
      throw new Error(`Streaming request failed (${response.status})`);
    }

    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let buffer = '';

    while (true) {
      const { value, done } = await reader.read();
      if (done) {
        break;
      }

      buffer += decoder.decode(value, { stream: true });
      const lines = buffer.split('\n');
      buffer = lines.pop() ?? '';

      for (const line of lines) {
        const trimmed = line.trim();
        if (!trimmed.startsWith('data:')) {
          continue;
        }

        const jsonText = trimmed.slice(5).trim();
        if (!jsonText) {
          continue;
        }

        const payload = JSON.parse(jsonText) as {
          token?: string;
          done?: boolean;
          sanitizedReply?: string;
          error?: string;
        };

        if (payload.error) {
          throw new Error(payload.error);
        }

        if (payload.token) {
          onToken(payload.token);
        }

        if (payload.done) {
          if (payload.sanitizedReply && onComplete) {
            onComplete(payload.sanitizedReply);
          }
          return;
        }
      }
    }
  }
}
