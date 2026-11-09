from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.problem_details import ProblemDetails
from ...models.viewport_screenshot import ViewportScreenshot
from ...models.viewport_screenshot_request import ViewportScreenshotRequest
from typing import cast



def _get_kwargs(
    *,
    body: ViewportScreenshotRequest,

) -> dict[str, Any]:
    headers: dict[str, Any] = {}


    

    

    _kwargs: dict[str, Any] = {
        "method": "post",
        "url": "/api/v1/viewport/screenshot",
    }

    _kwargs["json"] = body.to_dict()

    headers["Content-Type"] = "application/json"

    _kwargs["headers"] = headers
    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx.Response) -> ProblemDetails | ViewportScreenshot | None:
    if response.status_code == 200:
        response_200 = ViewportScreenshot.from_dict(response.json())



        return response_200

    if response.status_code == 422:
        response_422 = ProblemDetails.from_dict(response.json())



        return response_422

    if response.status_code == 503:
        response_503 = ProblemDetails.from_dict(response.json())



        return response_503

    if client.raise_on_unexpected_status:
        raise errors.UnexpectedStatus(response.status_code, response.content)
    else:
        return None


def _build_response(*, client: AuthenticatedClient | Client, response: httpx.Response) -> Response[ProblemDetails | ViewportScreenshot]:
    return Response(
        status_code=HTTPStatus(response.status_code),
        content=response.content,
        headers=response.headers,
        parsed=_parse_response(client=client, response=response),
    )


def sync_detailed(
    *,
    client: AuthenticatedClient | Client,
    body: ViewportScreenshotRequest,

) -> Response[ProblemDetails | ViewportScreenshot]:
    """ Writes the picture the viewport shows now to a PNG on the Server's machine, with its camera, display
    settings and look, at the size asked for or the one clients asked for.

    Args:
        body (ViewportScreenshotRequest): Asks for the viewport's picture as a PNG on the Server's
            machine: the camera, display settings and look it has
            now, at a size of its own. Nothing about it reaches a client's stream.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | ViewportScreenshot]
     """


    kwargs = _get_kwargs(
        body=body,

    )

    response = client.get_httpx_client().request(
        **kwargs,
    )

    return _build_response(client=client, response=response)

def sync(
    *,
    client: AuthenticatedClient | Client,
    body: ViewportScreenshotRequest,

) -> ProblemDetails | ViewportScreenshot | None:
    """ Writes the picture the viewport shows now to a PNG on the Server's machine, with its camera, display
    settings and look, at the size asked for or the one clients asked for.

    Args:
        body (ViewportScreenshotRequest): Asks for the viewport's picture as a PNG on the Server's
            machine: the camera, display settings and look it has
            now, at a size of its own. Nothing about it reaches a client's stream.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | ViewportScreenshot
     """


    return sync_detailed(
        client=client,
body=body,

    ).parsed

async def asyncio_detailed(
    *,
    client: AuthenticatedClient | Client,
    body: ViewportScreenshotRequest,

) -> Response[ProblemDetails | ViewportScreenshot]:
    """ Writes the picture the viewport shows now to a PNG on the Server's machine, with its camera, display
    settings and look, at the size asked for or the one clients asked for.

    Args:
        body (ViewportScreenshotRequest): Asks for the viewport's picture as a PNG on the Server's
            machine: the camera, display settings and look it has
            now, at a size of its own. Nothing about it reaches a client's stream.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | ViewportScreenshot]
     """


    kwargs = _get_kwargs(
        body=body,

    )

    response = await client.get_async_httpx_client().request(
        **kwargs
    )

    return _build_response(client=client, response=response)

async def asyncio(
    *,
    client: AuthenticatedClient | Client,
    body: ViewportScreenshotRequest,

) -> ProblemDetails | ViewportScreenshot | None:
    """ Writes the picture the viewport shows now to a PNG on the Server's machine, with its camera, display
    settings and look, at the size asked for or the one clients asked for.

    Args:
        body (ViewportScreenshotRequest): Asks for the viewport's picture as a PNG on the Server's
            machine: the camera, display settings and look it has
            now, at a size of its own. Nothing about it reaches a client's stream.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | ViewportScreenshot
     """


    return (await asyncio_detailed(
        client=client,
body=body,

    )).parsed
