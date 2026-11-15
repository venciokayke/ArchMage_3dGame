# Underdog 3D — checkpoints de desenvolvimento

## Como retomar

Leia este arquivo antes de implementar. Consulte git status e git log; preserve trabalho local. Nao refaca checkpoints concluidos. Nao execute Prepare Survival Game para aplicar apenas IA: ele recria a partida. Camera, arena, HUD, habilidades, vida/dano e WaveManager estao fora do escopo desta etapa.

## CP0 — baseline e analise (2026-11-15)

Estado inicial: branch mage, HEAD d966bbb. A sobrevivencia aprovada ainda estava em arquivos locais modificados/novos: runtime, assets gerados em Assets/Generated/UnderdogSurvival, cena UnderdogSurvival, pacotes, Build Settings e relatorios anteriores. Sera criado um commit local de baseline com esses arquivos antes dos commits de IA; nenhum descarte ou push.

Analise concluida:
- EnemyAI decide perseguir/atacar por distancia e reenvia o destino do jogador a cada 0.2 s. Ranged apenas usa um ponto atras de si para recuar.
- Todos os agentes partilham destino e prioridade padrao; nao ha reserva de posicoes, separacao local ou identidade de flanco. Isso favorece filas e congestionamento.
- Agent usa updatePosition=false e updateRotation=false; CharacterController.Move aplica desiredVelocity e gravidade; agent.nextPosition e sincronizado ao fim do frame. Preservar essa divisao.
- ArenaNavigation apenas carrega/remove NavMeshData; WaveManager controla as cinco ondas e spawns. Nenhum dos dois precisa mudar.
- Prefabs gerados: Melee (100 HP/2.5), Fast (60 HP/4), Ranged (75 HP/2.6), avoidance HighQuality. Os novos valores solicitados serao aplicados por comando seguro de Editor aos prefabs referenciados pelo manager, sem regravar cena ou ondas.

Proximas etapas:
1. CP1: destinos validos distribuidos, separacao local e atualizacoes escalonadas; preservar ataques/animacoes e validar compilacao.
2. CP2: flanqueamento e ranged com histerese/linha de tiro; comando seguro de presets e compilacao.
3. CP3: testes automatizados de navegacao/grupos/combate, regressao das ondas inalteradas, instrucoes e commit final.

Testes pendentes: perseguicao individual, melee em grupo, flanco, ranged em paredes/cobertura, grupo misto, dash do jogador e 15–25 agentes. Compilar nao significa validar gameplay ou visual.

## CP1 — runtime de posicionamento (em validacao)

Baseline confirmado: 4011aaf (sobrevivencia aprovada e CP0 ja registrados; nao houve necessidade de duplicar o commit).
Arquivos em trabalho: EnemyAI.cs e EnemyNavigationChecks.cs (+ meta).
Implementado: destinos reservados em aneis, separacao local no mesmo grupo/alvo, prioridades de avoidance distintas, decisoes escalonadas com intervalo minimo, limite de caminhos por decisao, parametros do Animator em cache e velocidade medida do CharacterController. Ataques continuam por seus eventos/timers existentes e pausam a navegacao sem cancelar animacoes. Rogue usa waypoint lateral antes de entrar no anel; Mage usa faixa ideal, histerese e procura posicoes com LOS no NavMesh.
Compilacao passou contra Unity 2022.3.62f3. Avisos anteriores: NavigationStatic e ItemEffect.
Falta antes de concluir CP1: suite real de Play Mode (individual, grupos melee, flanco, ranged/bordas/cobertura, grupo misto 25, dash e morte). Ajustar somente falhas observadas. Nenhum arquivo de ondas, arena, camera ou HUD foi alterado.
Depois: CP2 presets seguros para os prefabs existentes e ajuste somente da taxa da locomocao nos Controllers gerados; CP3 regressao/relatorios/instrucoes.
Validacao parcial CP1: individuo, oito melee, flanco, faixa ranged e recuo na borda passaram no runner Unity. Cenário com parede/LOS falhou; diagnostico de posicao/rota adicionado, investigacao pendente. Corrigida tambem inicializacao nativa de NavMeshPath para Start (Unity nao permite no inicializador de MonoBehaviour). Grupo 25 ainda nao executado ate superar LOS. Nao marcar CP1 como concluido antes da suite completa.

### CP1 concluido — navegacao e comportamentos

Validacao final em Unity 2022.3.62f3, projeto isolado, runner PlayMode com passo simulado de 1/60 s: PASS. A falha parcial de deslocamento vinha da fixture a 5000 unidades com passos diminutos no batch headless; corrigida a fixture para 200 unidades e o passo do runner, sem mudar a taxa do jogo. Parede/LOS tambem passou. Corrigida a tolerancia final de chegada melee para nao estacionar fora do alcance de ataque.

Testes executados: guerreiro individual; alcance final e pausa durante golpe; evento duplicado sem dano extra; cooldown; alvo escapando do alcance; cancelamento por morte; oito melee com destinos distintos; flanco lateral e aproximacao final; ranged aproximando/recuando; recuo junto da borda; contorno de cobertura para obter LOS; 25 agentes mistos sem sobreposicao persistente e dentro do NavMesh; alvo deslocado como dash; morte liberando reservas e colliders. Grupo misto: 1000 decisoes e 265 caminhos em 14 s simulados (nao e benchmark de CPU).

Arquivos do checkpoint: Assets/Scripts/Enemy/EnemyAI.cs, Assets/Tests/Editor/EnemyNavigationChecks.cs (+ meta), Docs/Validation/EnemyAI-PlayMode.xml e este documento. Compilacao Roslyn contra assemblies da Unity passou; avisos anteriores permanecem. Cena, ondas, prefabs, animacoes, vida/dano, camera e HUD nao foram regravados.

Falta: CP2 comando de Editor para aplicar HP/velocidades/perfis nos tres prefabs referenciados pelo WaveManager e ajustar apenas locomocao; CP3 regressao, instrucoes e relatorios. Testes visuais na arena aprovada, Hit/Death com os modelos reais e sensacao de combate continuam pendentes; a suite usa atores sinteticos para isolar navegacao e dano/eventos.
