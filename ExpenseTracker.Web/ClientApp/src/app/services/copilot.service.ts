import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environment';

export interface ExecutedTool {
  toolName: string;
  argumentsJson: string;
  output: string;
  success: boolean;
}

export interface ChatMessage {
  id: string;
  role: 'user' | 'assistant' | 'error';
  content: string;
  timestamp: Date;
  toolsExecuted?: ExecutedTool[];
}

export interface CopilotChatRequest {
  message: string;
  history?: Array<{ role: string; content: string }>;
}

export interface CopilotChatResponse {
  success: boolean;
  reply: string;
  toolsExecuted: ExecutedTool[];
  error?: string;
}

@Injectable({
  providedIn: 'root',
})
export class CopilotService {
  private readonly endpoint = `${environment.api}/api/copilot/chat`;

  constructor(private readonly http: HttpClient) {}

  chat(request: CopilotChatRequest): Observable<CopilotChatResponse> {
    return this.http.post<CopilotChatResponse>(this.endpoint, request);
  }
}
