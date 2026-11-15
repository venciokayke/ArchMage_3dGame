# Expansão do combate e buffs

Unity 2022.3.62f3 / URP. A implementação amplia os componentes existentes. Não há pacote novo, alteração de câmera, reconstrução da arena ou mudança na composição das cinco ondas. A IA coletiva e o Sprint/Stamina encontrados no projeto foram preservados; não foram adicionados buffs de Stamina.

## Aplicar na partida existente

1. Pare o Play Mode e aguarde a compilação. Abra `Assets/Scenes/UnderdogSurvival.unity` e salve suas alterações pendentes na cena, prefabs e Controllers.
2. Execute **Tools > Underdog > Prepare Combat Expansion**. Confira os caminhos concretos da cena e dos três prefabs na confirmação.
3. A ferramenta cria `Assets/Generated/CombatExpansion_<data>` com `SceneBefore.unity`, `Before_0/1/2.prefab`, Controllers copiados, clips especiais, máscara superior do Mage, material do aviso e prefab da rajada. Configura os prefabs referenciados pelo WaveManager e salva a cena atual com PlayerBuffs e explosão atualizada. Não executar **Prepare Survival Game** para aplicar esta etapa.
4. Execute **Tools > Underdog > Check Combat Expansion Setup (Edit Mode)**. O Console deve informar `PASS Combat Setup`.
5. Inicie uma nova sessão de Play Mode. Inicie a partida pelo botão existente ou Enter. Os ataques básicos e comandos do jogador continuam nos controles atuais.

O preparo usa AssetDatabase, PrefabUtility, AnimatorController, AvatarMask, Undo e EditorSceneManager. Recusa Play Mode, cena não salva, Controllers/prefabs com alterações pendentes, Prefab Mode sujo e prefabs originais KayKit. Cada execução cria outra pasta de backup; não duplica componentes nem camadas. Alterações personalizadas nas configurações existentes dos especiais são mantidas.

Os GUIDs dos prefabs e referências já existentes são preservados. Controllers e assets KayKit originais não são modificados. Em falha de gravação, restaura os prefabs e a cena pelos backups; os assets gerados ficam na pasta de recuperação. Para voltar manualmente, pare Play, copie o conteúdo dos backups sobre os arquivos originais correspondentes, preservando os `.meta` originais, e reabra a cena. Não substitua os `.meta` pelos dos backups.

**Nesta sessão, a ferramenta foi aplicada somente à cópia isolada de validação.** A cena, os três prefabs e o Player.controller do projeto principal mantêm os hashes de antes desta implementação. A configuração estrutural da partida principal requer o menu acima; não precisa regenerar arena ou refazer o NavMesh.

## Inimigos

Os ataques normais continuam com seu Animation Event `AplicarGolpe`, alcance, linha de visão e cooldown. Nenhum especial pode começar enquanto há ataque básico em execução; também bloqueia outro básico até terminar. Morte, desarme e empurrão cancelam o preparo e escondem o aviso.

| Arquétipo | Especial inicial | Entrada gradual |
| --- | --- | --- |
| Skeleton Warrior | Golpe pesado: 25 de dano, alcance 2,8, cone de 100°, preparo 0,9 s, recuperação 0,5 s, cooldown 6 s; empurrão 7 por 0,2 s | Onda 3 |
| Skeleton Rogue | Aviso vermelho 0,65 s, investida 13 unidades/s por 0,25 s, 15 de dano, recuperação 0,4 s, cooldown 5 s; ativação entre 3 e 8 unidades | Onda 4 |
| Skeleton Mage | Leque de três projéteis, 12 de dano por projétil, abertura de ±12°, preparo 0,65 s, recuperação 0,25 s, cooldown 7 s | Onda 5 |

No Inspector dos prefabs, **EnemySpecialAttack** expõe os grupos `pesado`, `investida` e `rajada`, incluindo `ondaMinima`, dano, tempos, alcance e referências visuais. `atrasoInicial` padrão 1–2 s e `investida.intervaloEntreRogues` 0,35 s mais variação até 0,15 s distribuem os inícios dos Rogues. Use valores não negativos e distância máxima maior que a mínima.

O Rogue acompanha o alvo durante o aviso; fixa a direção ao começar a investida e não corrige a trajetória. A detecção usa o segmento realmente percorrido, com no máximo um dano por investida. CharacterController faz a colisão física e o passo fica limitado ao NavMesh. O aviso representa a distância máxima de 3,25 unidades; uma parede pode encurtar o deslocamento real.

O Mage mantém pernas em locomação e usa `Upper Body` com máscara da hierarquia real `spine`, sem incluir pernas. Mira e rotação continuam voltadas ao jogador ao recuar ou mover lateralmente. Mantém as distâncias e decisões da IA existente, com linha de visão tanto para iniciar quanto para executar o disparo. Configuração em **EnemyAI**: `moverAoAtacar`, `camadaAtaque`, distâncias mínima/ideal/máxima, margem e intervalos de decisão/reposicionamento. A ferramenta configura `pontoImpactoEspecial` e `SpecialRate` para que o preparo corresponda ao ponto de impacto do clip.

Modelos preservados: **Skeleton_Warrior, Skeleton_Rogue e Skeleton_Mage** dos prefabs existentes. Clips reais reutilizados:

- `Idle_A`, `Running_A`, `Hit_A`, `Death_A`: estados já existentes.
- `Melee_1H_Attack_Chop`: básico e cópia de preparo do Warrior/Rogue.
- `Ranged_Magic_Shoot`: básico e cópia de preparo do Mage.
- `Walking_Backwards`, `Running_Strafe_Left`, `Running_Strafe_Right`: Blend Tree direcional das pernas do Mage, com `MoveX/MoveZ` calculados pelo deslocamento real.
- `Dodge_Forward`: investida do Rogue, ajustada à duração de 0,25 s.

Os clips novos são cópias. Clips de preparo especial não carregam `AplicarGolpe`, evitando dano/projétil básico duplicado. Se não houver `spine`, o preparo informa a alternativa de corpo inteiro; os modelos atuais suportaram a máscara nos testes. As animações e os Animation Events do jogador não são reconfigurados.

## Explosão e empurrões

**PlayerCombat > explosao**: dano base 50, `empurrao` 12, `duracaoEmpurrao` 0,3 s. Raio e cooldown existentes são preservados (padrões 4,5 e 4 s). O dano antigo padrão 35 migra para 50; valores personalizados diferentes de 35 são mantidos.

O dano continua deduplicado por Vida, atinge vários inimigos e respeita paredes. O impulso segue a direção radial a partir do jogador; não é aplicado a alvos mortos ou que não receberam dano. A instância do efeito existente recebe partículas maiores e emissão reforçada, sem modificar o prefab visual original.

**CombatDisplacement** apenas calcula o impulso amortecido. EnemyAI e PlayerMovement continuam responsáveis por uma única chamada CharacterController.Move por frame, com gravidade. No inimigo, a navegação pausa, o passo é limitado ao NavMesh e a rota é recalculada após o empurrão. O dash do jogador mantém seus parâmetros e custo existentes.

## Buffs e ondas

Após as ondas 1–4, WaveManager entra em `EscolhendoBuff`. Não há spawns nem início de próxima onda; movimento e magias do jogador ficam bloqueados durante a escolha. O painel apresenta três melhorias diferentes. Clique ou use **1/2/3** (também teclado numérico). Uma escolha válida aplica o efeito, mostra sua confirmação por 2 s e libera o intervalo da próxima onda. Cliques repetidos e índices inválidos são rejeitados. A quinta onda segue diretamente para a vitória existente.

| Buff | Por acúmulo |
| --- | --- |
| Poder Arcano | +20% de dano primário, explosão e rajada |
| Vitalidade | +25 de vida máxima e cura 25 HP |
| Agilidade | +10% de velocidade de movimento |
| Conjuração Rápida | -10% do cooldown das três magias |
| Explosão Aprimorada | +20% de dano da explosão e +15% de empurrão |
| Resistência | -10% do dano recebido |

**WaveManager > melhorias** referencia **PlayerBuffs**, onde o catálogo expõe nome, descrição, valores e `maxAcumulos` (padrão 4). As seis definições serializáveis ficam no componente, separadas do cálculo; não foi necessário criar ScriptableObjects para seis opções de uma partida.

Limites configuráveis em PlayerBuffs: bônus de dano geral +150%, dano próprio da explosão +120%, empurrão +60%, movimento +50%, redução de cooldown 50%, cooldown mínimo 0,15 s e resistência 60%. Cada tipo soma seus acúmulos sobre o valor base capturado uma vez no início. Poder Arcano e Explosão Aprimorada combinam fatores distintos: com um de cada, explosão = `50 × 1,2 × 1,2 = 72`. Não há multiplicação por frame. Dois Poderes Arcanos produzem `base × 1,4`, não `base × 1,2 × 1,2`.

Vida aplica a resistência no caminho de dano existente. Vitalidade reutiliza PlayerHp. Reiniciar recarrega a cena e restaura valores serializados/acúmulos zero. Morte durante seleção cancela o painel e termina a partida normalmente. Nenhum timescale é alterado para escolher buffs. O clique de escolha não dispara magia ao reabilitar PlayerCombat no mesmo frame.

A interface existente não foi redesenhada: BuffSelectionUI cria apenas o Canvas de escolha e confirmação sob o gerenciador, reutilizando o EventSystem existente. Não é necessário montar botões manualmente.

## Validação executada

- Compilação de todos os scripts runtime/Editor contra Unity 2022.3.62f3: aprovada; avisos preexistentes `NavigationStatic` obsoleto e campo `ItemEffect` não atribuído.
- **Unity Edit Mode: 3/3 aprovados**, na cópia real isolada: preparo via APIs do Editor, backups, GUIDs/fileIDs, Controllers originais, eventos, máscara, clips e preservação de câmera/HUD/ondas/dash; regressões de PlayerCombat e CombatFeedback. Relatório: [CombatExpansion-EditMode.xml](Validation/CombatExpansion-EditMode.xml).
- **Unity Play Mode: 8/8 aprovados**, na mesma cópia: especiais/empurrões, explosão multialvo, seis buffs/acúmulos/limites, 40 spawns nas cinco ondas com quatro escolhas e vitória, morte durante escolha, Mage real disparando por evento enquanto move pernas, Rogue real com aviso e Dodge, reinício real restaurando atributos, regressões de movimentação e grupos de 25 inimigos. Projéteis a 60/10 FPS e linha de visão fazem parte da regressão existente. Relatório: [CombatExpansion-PlayMode.xml](Validation/CombatExpansion-PlayMode.xml).

Os testes de gameplay combinaram fixtures isoladas de física/NavMesh com a cena/modelos reais copiados; não foram sessões humanas jogando as cinco ondas. Native Test Runner e alterações estruturais executados em `Temp/StartSceneVerification`, sem Pipeline e sem regravar os assets do projeto principal.

Menus permanentes para repetir verificações: **Check Combat Expansion Setup (Edit Mode)**; em Play, **Check Combat Expansion (Play Mode)** e **Check Buffs And Five Waves (Play Mode)**. Os dois últimos criam atores de teste temporários e fazem asserts, não simulam mouse/teclado físicos. Os testes reais de reinício/camadas foram executados pelo Native Test Runner na cópia isolada.

## Testes manuais pendentes

1. Na arena preparada, testar WASD, mira do cajado/mouse, dash e as três magias enquanto anda. Conferir `LancarMagia` sem disparos extras.
2. Observar Mage recuando e movendo lateralmente durante disparos; pernas, tronco, Hit e Death devem permanecer legíveis. Testar linha de visão nas colunas/paredes reais.
3. A partir das ondas 3/4/5, observar cada especial, sua preparação, cooldown e recuperação. Desviar lateralmente do Rogue durante o aviso; confirmar direção fixa e ausência de dano repetido. Matar ou empurrar durante o aviso deve removê-lo.
4. Usar explosão perto de grupos, paredes e bordas; verificar dano único, afastamento radial, colisão e retomada da navegação. Testar também investida contra obstáculos e contra o personagem.
5. Terminar ondas 1–4, conferir três opções distintas, testar botão/1–3 e cliques rápidos. Enquanto nenhuma opção for escolhida não pode surgir outro inimigo. Conferir buffs repetidos no Inspector durante Play.
6. Morrer, reiniciar e confirmar HP/dano/velocidade/resistência/acúmulos iniciais; vencer a quinta onda sem novo painel. A cena deve continuar habilitada nos Build Settings existentes para reiniciar.
7. Conferir contraste/transparência do aviso vermelho, intensidade dos efeitos e leitura do painel nas resoluções utilizadas. Medir Profiler com 15–25 inimigos; os testes de grupos não são benchmark de CPU/GPU.

## Arquivos desta etapa (CP8–CP9)

- Novos runtime: `Assets/Scripts/Combat/CombatDisplacement.cs`, `Assets/Scripts/Enemy/EnemySpecialAttack.cs`, `Assets/Scripts/Game/PlayerBuffs.cs`, `Assets/Scripts/Game/BuffSelectionUI.cs` e respectivos `.meta`.
- Runtime ampliado: `EnemyAI.cs`, `PlayerMovement.cs` (somente integração do impulso), `PlayerCombat.cs`, `Vida.cs`, `WaveManager.cs`. `Projectile.cs` foi analisado e reutilizado sem alterações.
- Editor: novo `Assets/Editor/CombatExpansionSetup.cs` (+ `.meta`); `SurvivalGameSetup.cs` só recebe o dano inicial 50 para gerações futuras.
- Verificações: novos `BuffChecks.cs`, `CombatExpansionChecks.cs`, `CombatExpansionSetupChecks.cs` (+ `.meta`) em `Assets/Tests/Editor`; `SurvivalChecks.cs` ajustado ao dano 50 e à escolha intermediária obrigatória.
- Documentação: este guia, `DESENVOLVIMENTO.md` e relatórios `CombatExpansion-*.xml` em `Docs/Validation`.

As alterações locais do usuário em cenas, prefabs, Controller do jogador, backups/presets e exclusões preexistentes foram preservadas e não fazem parte dos commits de implementação. Nenhum push ou merge foi realizado.
